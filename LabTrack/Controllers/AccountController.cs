using System.Text.RegularExpressions;
using LabTrack.Models;
using LabTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LabTrack.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public partial class AccountController(SignInManager<Usuario> signIn, UserManager<Usuario> users) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Home") : View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var login = model.Identificador.Trim();
        var user = await users.FindByNameAsync(login);
        if (user is null || !user.Activo ||
            (user.CodigoEstudiante is not null && !StudentCode().IsMatch(login)))
        {
            ModelState.AddModelError("", "Credenciales inválidas");
            return View(model);
        }
        var result = await signIn.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
            return RedirectToAction("Index", "Home");
        }
        ModelState.AddModelError("", result.IsLockedOut
            ? "Alerta de seguridad: cuenta bloqueada temporalmente por tres intentos fallidos. Inténtalo al finalizar el bloqueo."
            : "Credenciales inválidas");
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [GeneratedRegex(@"^C00[0-9]{6}$")]
    private static partial Regex StudentCode();
}
