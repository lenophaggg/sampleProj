using diary.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace diary.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public HomeController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                var user = _userManager.GetUserAsync(User).Result;
                if (user != null)
                {
                    if (_userManager.IsInRoleAsync(user, "Admin").Result)
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else if (_userManager.IsInRoleAsync(user, "GroupHead").Result)
                    {
                        return RedirectToAction("Index", "GroupHead");
                    }
                    else if (_userManager.IsInRoleAsync(user, "Teacher").Result)
                    {
                        return RedirectToAction("Index", "Teacher");
                    }
                }
            }

            return View();
        }

        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByNameAsync(model.Login);
            if (user == null)
            {
                ModelState.AddModelError("", "�������� ����� ��� ������.");
                return View(model);
            }

            // ���������, ������������ �� ������������
            if (await _userManager.IsLockedOutAsync(user))
            {
                ModelState.AddModelError("", "���� ������� ������ �������������. ���������� ����� ����� 3 ������.");
                return View(model);
            }

            // ������� ����� � ����������� ��� ��������� ��������
            var signInResult = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (signInResult.Succeeded)
            {
                // �������� ����� ������������ � ���������������
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                else if (await _userManager.IsInRoleAsync(user, "GroupHead"))
                {
                    return RedirectToAction("Index", "GroupHead");
                }
                else if (await _userManager.IsInRoleAsync(user, "Teacher"))
                {
                    return RedirectToAction("Index", "Teacher");
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            // ���������, ��� �� ������������ ������������
            if (signInResult.IsLockedOut)
            {
                ModelState.AddModelError("", "���� ������� ������ �������������.");
            }
            else if (signInResult.IsNotAllowed)
            {
                ModelState.AddModelError("", "���� � ������� ��������.");
            }
            else if (signInResult.RequiresTwoFactor)
            {
                ModelState.AddModelError("", "��������� ������������� ��������������.");
            }
            else
            {
                ModelState.AddModelError("", "�������� ����� ��� ������.");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
