using System.ComponentModel.DataAnnotations;
using Argos.Application.Access;
using Argos.Application.Common.Abstractions;
using Argos.Application.Common.Exceptions;
using Argos.Domain.Access;

namespace Argos.Application.Seeding;

/// <summary>
/// Reconcilia os usuários de demonstração com a configuração (RF-053; D-40; plan 14.4, 14.10): cria o que falta e,
/// num usuário existente, atualiza nome, perfil e hash quando mudaram. Não reativa nem inativa. Uma segunda execução
/// com a mesma configuração não grava nada.
/// </summary>
public sealed class DemoUserProvisioningService(IUserRepository users, IPasswordHasher passwordHasher, IUnitOfWork unitOfWork)
{
    public async Task<DemoUserProvisioningResult> ProvisionAsync(
        DemoUsersOptions options,
        CancellationToken cancellationToken = default)
    {
        var demoUsers = Validate(options);
        var created = new List<string>();
        var updated = new List<string>();

        foreach (var (role, demoUser) in demoUsers)
        {
            var login = User.NormalizeLogin(demoUser.Login);
            var user = await users.FindByLoginAsync(login, cancellationToken);

            if (user is null)
            {
                users.Add(User.Create(demoUser.Name, login, passwordHasher.Hash(demoUser.Password), role));
                created.Add(login);
                continue;
            }

            var changed = user.UpdateProfile(demoUser.Name, role);
            if (!passwordHasher.Verify(user.PasswordHash, demoUser.Password))
            {
                user.ReplacePasswordHash(passwordHasher.Hash(demoUser.Password));
                changed = true;
            }

            if (changed)
            {
                updated.Add(login);
            }
        }

        if (created.Count > 0 || updated.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new DemoUserProvisioningResult(created, updated);
    }

    private static (UserRole Role, DemoUserOptions User)[] Validate(DemoUsersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new Dictionary<string, string[]>();
        var demoUsers = new List<(UserRole, DemoUserOptions)>();

        Add(UserRole.Administrator, "administrator", options.Administrator);
        Add(UserRole.Operator, "operator", options.Operator);
        Add(UserRole.Manager, "manager", options.Manager);

        if (errors.Count == 0)
        {
            var repeated = demoUsers
                .GroupBy(demoUser => User.NormalizeLogin(demoUser.Item2.Login))
                .FirstOrDefault(group => group.Count() > 1);
            if (repeated is not null)
            {
                errors["login"] = [$"O login \"{repeated.Key}\" foi configurado para mais de um perfil."];
            }
        }

        return errors.Count == 0
            ? demoUsers.ToArray()
            : throw new ValidationFailedException(errors);

        void Add(UserRole role, string prefix, DemoUserOptions? demoUser)
        {
            if (demoUser is null)
            {
                errors[prefix] = ["Configure o usuário de demonstração deste perfil."];
                return;
            }

            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(demoUser, new ValidationContext(demoUser), results, validateAllProperties: true))
            {
                demoUsers.Add((role, demoUser));
                return;
            }

            foreach (var result in results)
            {
                foreach (var member in result.MemberNames)
                {
                    errors[$"{prefix}.{char.ToLowerInvariant(member[0])}{member[1..]}"] = [result.ErrorMessage ?? "Valor inválido."];
                }
            }
        }
    }
}

/// <summary>Logins criados e atualizados numa reconciliação.</summary>
public sealed record DemoUserProvisioningResult(IReadOnlyList<string> Created, IReadOnlyList<string> Updated);
