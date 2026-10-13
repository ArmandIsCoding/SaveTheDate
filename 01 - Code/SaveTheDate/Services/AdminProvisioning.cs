using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using SaveTheDate.Domain;

namespace SaveTheDate.Services;

public static class AdminProvisioning
{
    public static bool IsCommand(string[] args) => args.Any(x => x is "--grant-super-admin" or "--revoke-super-admin");

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, TextWriter output)
    {
        if (args.Length != 2 || args[0] is not ("--grant-super-admin" or "--revoke-super-admin")
            || !new EmailAddressAttribute().IsValid(args[1].Trim()))
        {
            await output.WriteLineAsync("Uso: dotnet SaveTheDate.dll --grant-super-admin correo | --revoke-super-admin correo");
            return 1;
        }
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(args[1].Trim());
        if (user is null)
        {
            await output.WriteLineAsync("No se encontró una cuenta existente. Verificá el correo, la carpeta publicada y la conexión SQLite. No se creó ninguna cuenta.");
            return 1;
        }
        var grant = args[0] == "--grant-super-admin";
        if (grant && string.Equals(user.Email, "demo@savethedate.local", StringComparison.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync("La cuenta demo tiene acceso sin contraseña y no puede ser superadministradora. Usá una cuenta personal.");
            return 1;
        }
        if (await users.IsInRoleAsync(user, AdminAccess.Role) == grant)
        {
            await output.WriteLineAsync(grant ? "La cuenta ya tiene acceso de superadmin." : "La cuenta ya no tiene acceso de superadmin.");
            return 0;
        }
        if (grant && !await roles.RoleExistsAsync(AdminAccess.Role))
        {
            var created = await roles.CreateAsync(new IdentityRole(AdminAccess.Role));
            if (!created.Succeeded) throw new InvalidOperationException("No se pudo crear el rol administrativo.");
        }
        var result = grant ? await users.AddToRoleAsync(user, AdminAccess.Role) : await users.RemoveFromRoleAsync(user, AdminAccess.Role);
        if (!result.Succeeded) throw new InvalidOperationException("No se pudo actualizar el permiso administrativo.");
        await output.WriteLineAsync(grant
            ? "Acceso habilitado. Iniciá sesión con tu cuenta y abrí /administracion."
            : "Acceso revocado. Las próximas solicitudes administrativas se rechazarán, incluso con una sesión existente.");
        return 0;
    }
}
