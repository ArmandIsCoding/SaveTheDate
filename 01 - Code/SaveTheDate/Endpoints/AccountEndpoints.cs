using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using SaveTheDate.Domain;

namespace SaveTheDate.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/cuenta/ingresar", async (HttpContext context, IAntiforgery csrf, SignInManager<ApplicationUser> signIn) =>
        {
            await csrf.ValidateRequestAsync(context);
            var form = await context.Request.ReadFormAsync();
            var result = await signIn.PasswordSignInAsync(form["email"].ToString().Trim(), form["password"].ToString(), form["remember"] == "on", lockoutOnFailure: true);
            return result.Succeeded ? Results.LocalRedirect("/eventos") : Results.LocalRedirect("/ingresar?error=" + Uri.EscapeDataString(result.IsLockedOut ? "Esperá unos minutos antes de volver a intentar." : "Revisá tu correo y contraseña."));
        }).RequireRateLimiting("accounts");

        app.MapPost("/cuenta/registrar", async (HttpContext context, IAntiforgery csrf, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) =>
        {
            await csrf.ValidateRequestAsync(context);
            var form = await context.Request.ReadFormAsync();
            var name = form["name"].ToString().Trim(); var email = form["email"].ToString().Trim(); var password = form["password"].ToString();
            string? error = null;
            if (name.Length is < 2 or > 80 || email.Length > 200 || !new EmailAddressAttribute().IsValid(email)) error = "Revisá tu nombre y correo electrónico.";
            else if (password != form["confirm"]) error = "Las contraseñas no coinciden.";
            else if (password.Length is < 10 or > 128) error = "La contraseña debe tener entre 10 y 128 caracteres.";
            if (error is not null) return Results.LocalRedirect("/registro?error=" + Uri.EscapeDataString(error));
            var user = new ApplicationUser { UserName = email, Email = email, DisplayName = name };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded) return Results.LocalRedirect("/registro?error=" + Uri.EscapeDataString("No pudimos crear la cuenta. Revisá los datos o probá iniciar sesión."));
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.LocalRedirect("/eventos");
        }).RequireRateLimiting("accounts");

        app.MapPost("/cuenta/salir", async (HttpContext context, IAntiforgery csrf, SignInManager<ApplicationUser> signIn) =>
        {
            await csrf.ValidateRequestAsync(context); await signIn.SignOutAsync(); return Results.LocalRedirect("/ingresar");
        }).RequireAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/cuenta/demo", async (HttpContext context, IAntiforgery csrf, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) =>
            {
                await csrf.ValidateRequestAsync(context);
                var user = await users.FindByEmailAsync("demo@savethedate.local");
                if (user is null) return Results.NotFound();
                await signIn.SignInAsync(user, false); return Results.LocalRedirect("/eventos");
            }).RequireRateLimiting("accounts");
        }
    }
}
