using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using HealthConnect.Data;
using HealthConnect.Models;

namespace HealthConnect.Controllers
{
    public class AccountController : Controller
    {
        private readonly DatabaseHelper _db;
        private readonly IWebHostEnvironment _env;

        public AccountController(DatabaseHelper db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Dashboard", "Home");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var user = await _db.QuerySingleAsync(
                    "SELECT UserId, FullName, Email, PasswordHash, Role, IsActive FROM Users WHERE Email = @Email",
                    new SqlParameter("@Email", model.Email));

                if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user["PasswordHash"].ToString()))
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                if (!(bool)user["IsActive"])
                {
                    ModelState.AddModelError("", "Your account has been suspended. Contact support.");
                    return View(model);
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user["UserId"].ToString()!),
                    new Claim(ClaimTypes.Name, user["FullName"].ToString()!),
                    new Claim(ClaimTypes.Email, user["Email"].ToString()!),
                    new Claim(ClaimTypes.Role, user["Role"].ToString()!)
                };

                var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                    new AuthenticationProperties { IsPersistent = model.RememberMe });

                await _db.ExecuteNonQueryAsync(
                    "INSERT INTO AuditLog (UserId, Action, Details) VALUES (@UserId, 'Login', 'Successful login')",
                    new SqlParameter("@UserId", user["UserId"]));

                var role = user["Role"].ToString();
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return role == "Admin"
                    ? RedirectToAction("Index", "Admin")
                    : RedirectToAction("Dashboard", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Dashboard", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var existing = await _db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM Users WHERE Email = @Email",
                    new SqlParameter("@Email", model.Email));

                if (existing > 0)
                {
                    ModelState.AddModelError("Email", "An account with this email already exists.");
                    return View(model);
                }

                var hash = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 11);

                await _db.ExecuteNonQueryAsync(
                    @"INSERT INTO Users (FullName, Email, PasswordHash, Phone, DateOfBirth, Gender, Role)
                      VALUES (@FullName, @Email, @Hash, @Phone, @DOB, @Gender, 'Patient')",
                    new SqlParameter("@FullName", model.FullName),
                    new SqlParameter("@Email",    model.Email),
                    new SqlParameter("@Hash",     hash),
                    new SqlParameter("@Phone",    (object?)model.Phone    ?? DBNull.Value),
                    new SqlParameter("@DOB",      (object?)model.DateOfBirth ?? DBNull.Value),
                    new SqlParameter("@Gender",   (object?)model.Gender   ?? DBNull.Value));

                TempData["Success"] = "Account created! Please log in.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Registration failed. Please try again.");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.QuerySingleAsync(
                "SELECT * FROM Users WHERE UserId = @UserId",
                new SqlParameter("@UserId", userId));

            if (user == null) return NotFound();

            var model = new ProfileEditViewModel
            {
                FullName         = user["FullName"].ToString()!,
                Phone            = user["Phone"]?.ToString(),
                DateOfBirth      = user["DateOfBirth"] as DateTime?,
                Gender           = user["Gender"]?.ToString(),
                Address          = user["Address"]?.ToString(),
                EmergencyContact = user["EmergencyContact"]?.ToString(),
                MedicalNotes     = user["MedicalNotes"]?.ToString(),
            };

            ViewBag.Email        = user["Email"].ToString();
            ViewBag.ProfileImagePath = user["ProfileImagePath"]?.ToString() ?? "/images/default-avatar.png";
            ViewBag.MemberSince  = ((DateTime)user["CreatedAt"]).ToString("MMMM yyyy");
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileEditViewModel model)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!ModelState.IsValid)
            {
                ViewBag.Email = User.FindFirstValue(ClaimTypes.Email);
                return View(model);
            }

            try
            {
                // Handle profile image upload
                string? imagePath = null;
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    var allowed = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                    if (!allowed.Contains(model.ProfileImage.ContentType))
                    {
                        ModelState.AddModelError("ProfileImage", "Invalid file type.");
                        return View(model);
                    }
                    if (model.ProfileImage.Length > 2 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ProfileImage", "Image must be under 2 MB.");
                        return View(model);
                    }

                    var uploads = Path.Combine(_env.WebRootPath, "uploads");
                    Directory.CreateDirectory(uploads);
                    var fileName = Guid.NewGuid() + Path.GetExtension(model.ProfileImage.FileName);
                    var filePath = Path.Combine(uploads, fileName);
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await model.ProfileImage.CopyToAsync(stream);
                    imagePath = "/uploads/" + fileName;
                }

                // Handle password change
                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    var user = await _db.QuerySingleAsync(
                        "SELECT PasswordHash FROM Users WHERE UserId = @UserId",
                        new SqlParameter("@UserId", userId));

                    if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user!["PasswordHash"].ToString()))
                    {
                        ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                        return View(model);
                    }

                    var newHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, 11);
                    await _db.ExecuteNonQueryAsync(
                        "UPDATE Users SET PasswordHash = @Hash WHERE UserId = @UserId",
                        new SqlParameter("@Hash",   newHash),
                        new SqlParameter("@UserId", userId));
                }

                var sql = imagePath != null
                    ? @"UPDATE Users SET FullName=@FullName, Phone=@Phone, DateOfBirth=@DOB,
                        Gender=@Gender, Address=@Address, EmergencyContact=@EC,
                        MedicalNotes=@Notes, ProfileImagePath=@Img WHERE UserId=@UserId"
                    : @"UPDATE Users SET FullName=@FullName, Phone=@Phone, DateOfBirth=@DOB,
                        Gender=@Gender, Address=@Address, EmergencyContact=@EC,
                        MedicalNotes=@Notes WHERE UserId=@UserId";

                var parameters = new List<SqlParameter>
                {
                    new("@FullName", model.FullName),
                    new("@Phone",    (object?)model.Phone            ?? DBNull.Value),
                    new("@DOB",      (object?)model.DateOfBirth      ?? DBNull.Value),
                    new("@Gender",   (object?)model.Gender           ?? DBNull.Value),
                    new("@Address",  (object?)model.Address          ?? DBNull.Value),
                    new("@EC",       (object?)model.EmergencyContact ?? DBNull.Value),
                    new("@Notes",    (object?)model.MedicalNotes     ?? DBNull.Value),
                    new("@UserId",   userId),
                };
                if (imagePath != null) parameters.Add(new("@Img", imagePath));

                await _db.ExecuteNonQueryAsync(sql, parameters.ToArray());

                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Update failed. Please try again.");
                return View(model);
            }
        }

        public IActionResult AccessDenied() => View();
    }
}