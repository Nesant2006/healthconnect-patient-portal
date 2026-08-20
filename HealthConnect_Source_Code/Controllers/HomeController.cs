using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using HealthConnect.Data;
using HealthConnect.Models;

namespace HealthConnect.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseHelper _db;

        public HomeController(DatabaseHelper db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var doctors = await _db.QueryAsync(
                @"SELECT TOP 6 d.DoctorId, d.FullName, s.Name AS Specialty,
                  d.ConsultationFee, d.Rating, d.ReviewCount
                  FROM Doctors d
                  JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                  WHERE d.IsAvailable = 1
                  ORDER BY d.Rating DESC");

            var featuredDoctors = doctors.Select(r => new Doctor
            {
                DoctorId        = Convert.ToInt32(r["DoctorId"]),
                FullName        = r["FullName"].ToString()!,
                Specialty       = r["Specialty"].ToString()!,
                ConsultationFee = Convert.ToDecimal(r["ConsultationFee"]),
                Rating          = Convert.ToDouble(r["Rating"]),
                ReviewCount     = Convert.ToInt32(r["ReviewCount"]),
            }).ToList();

            return View(featuredDoctors);
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var rows = await _db.QueryAsync(
                @"SELECT a.AppointmentId, a.PatientId, a.DoctorId,
                  u.FullName AS PatientName, u.Email AS PatientEmail,
                  d.FullName AS DoctorName, s.Name AS Specialty,
                  d.ConsultationFee, a.AppointmentDate, a.AppointmentTime,
                  a.ReasonForVisit, a.Status, a.IsUrgent, a.CreatedAt
                  FROM Appointments a
                  JOIN Users u ON a.PatientId = u.UserId
                  JOIN Doctors d ON a.DoctorId = d.DoctorId
                  JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                  WHERE a.PatientId = @UserId
                  ORDER BY a.AppointmentDate DESC",
                new SqlParameter("@UserId", userId));

            var all = rows.Select(r => new Appointment
            {
                AppointmentId   = Convert.ToInt32(r["AppointmentId"]),
                PatientId       = Convert.ToInt32(r["PatientId"]),
                DoctorId        = Convert.ToInt32(r["DoctorId"]),
                PatientName     = r["PatientName"].ToString()!,
                DoctorName      = r["DoctorName"].ToString()!,
                Specialty       = r["Specialty"].ToString()!,
                ConsultationFee = Convert.ToDecimal(r["ConsultationFee"]),
                AppointmentDate = Convert.ToDateTime(r["AppointmentDate"]),
                AppointmentTime = r["AppointmentTime"].ToString()!,
                ReasonForVisit  = r["ReasonForVisit"]?.ToString(),
                Status          = r["Status"].ToString()!,
                IsUrgent        = Convert.ToBoolean(r["IsUrgent"]),
                CreatedAt       = Convert.ToDateTime(r["CreatedAt"]),
            }).ToList();

            var notifications = await _db.QueryAsync(
                @"SELECT TOP 5 NotificationId, Title, Message, IsRead, CreatedAt
                  FROM Notifications WHERE UserId = @UserId
                  ORDER BY CreatedAt DESC",
                new SqlParameter("@UserId", userId));

            var model = new DashboardViewModel
            {
                TotalAppointments     = all.Count,
                PendingAppointments   = all.Count(a => a.Status == "Pending"),
                ConfirmedAppointments = all.Count(a => a.Status == "Confirmed"),
                CompletedAppointments = all.Count(a => a.Status == "Completed"),
                CancelledAppointments = all.Count(a => a.Status == "Cancelled"),
                UpcomingAppointments  = all.Where(a => a.AppointmentDate >= DateTime.Today
                                            && a.Status != "Cancelled").Take(5).ToList(),
                PastAppointments      = all.Where(a => a.AppointmentDate < DateTime.Today
                                            || a.Status == "Completed").Take(10).ToList(),
                Notifications         = notifications.Select(n => new Notification
                {
                    NotificationId = Convert.ToInt32(n["NotificationId"]),
                    Title          = n["Title"].ToString()!,
                    Message        = n["Message"].ToString()!,
                    IsRead         = Convert.ToBoolean(n["IsRead"]),
                    CreatedAt      = Convert.ToDateTime(n["CreatedAt"]),
                }).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> Doctors(string? specialty, string? search)
        {
            var where      = "WHERE d.IsAvailable = 1";
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrEmpty(specialty))
            {
                where += " AND s.Name = @Specialty";
                parameters.Add(new SqlParameter("@Specialty", specialty));
            }

            if (!string.IsNullOrEmpty(search))
            {
                where += " AND (d.FullName LIKE @Search OR s.Name LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            var rows = await _db.QueryAsync(
                $@"SELECT d.DoctorId, d.FullName, s.Name AS Specialty,
                   d.ConsultationFee, d.Rating, d.ReviewCount,
                   d.Qualifications, d.ExperienceYears, d.Languages
                   FROM Doctors d
                   JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                   {where}
                   ORDER BY d.Rating DESC",
                parameters.ToArray());

            var doctors = rows.Select(r => new Doctor
            {
                DoctorId        = Convert.ToInt32(r["DoctorId"]),
                FullName        = r["FullName"].ToString()!,
                Specialty       = r["Specialty"].ToString()!,
                ConsultationFee = Convert.ToDecimal(r["ConsultationFee"]),
                Rating          = Convert.ToDouble(r["Rating"]),
                ReviewCount     = Convert.ToInt32(r["ReviewCount"]),
                Qualifications  = r["Qualifications"]?.ToString(),
                ExperienceYears = Convert.ToInt32(r["ExperienceYears"]),
                Languages       = r["Languages"]?.ToString(),
            }).ToList();

            var specialties = await _db.QueryAsync(
                "SELECT Name FROM Specialties ORDER BY Name");

            ViewBag.Specialties       = specialties.Select(s => s["Name"].ToString()).ToList();
            ViewBag.SelectedSpecialty = specialty;
            ViewBag.SearchTerm        = search;

            return View(doctors);
        }

        public IActionResult Error(int? statusCode)
        {
            var model = new ErrorViewModel
            {
                RequestId  = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = statusCode ?? 500
            };
            return View(model);
        }
    }

    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DatabaseHelper _db;

        public AdminController(DatabaseHelper db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var stats = await _db.QuerySingleAsync(
                @"SELECT
                  (SELECT COUNT(*) FROM Users WHERE Role='Patient') AS TotalPatients,
                  (SELECT COUNT(*) FROM Appointments) AS TotalAppointments,
                  (SELECT COUNT(*) FROM Appointments WHERE Status='Pending') AS PendingAppointments,
                  (SELECT COUNT(*) FROM Appointments WHERE CAST(AppointmentDate AS DATE) = CAST(GETDATE() AS DATE)) AS TodayAppointments");

            var recentRows = await _db.QueryAsync(
                @"SELECT TOP 10 a.AppointmentId, a.PatientId, a.DoctorId,
                  u.FullName AS PatientName, u.Email AS PatientEmail,
                  d.FullName AS DoctorName, s.Name AS Specialty,
                  d.ConsultationFee, a.AppointmentDate, a.AppointmentTime,
                  a.ReasonForVisit, a.Status, a.IsUrgent, a.CreatedAt
                  FROM Appointments a
                  JOIN Users u ON a.PatientId = u.UserId
                  JOIN Doctors d ON a.DoctorId = d.DoctorId
                  JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                  ORDER BY a.CreatedAt DESC");

            var specialtyRows = await _db.QueryAsync(
                @"SELECT s.Name, COUNT(*) AS Total
                  FROM Appointments a
                  JOIN Doctors d ON a.DoctorId = d.DoctorId
                  JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                  GROUP BY s.Name ORDER BY Total DESC");

            var model = new AdminDashboardViewModel
            {
                TotalPatients       = Convert.ToInt32(stats!["TotalPatients"]),
                TotalAppointments   = Convert.ToInt32(stats["TotalAppointments"]),
                PendingAppointments = Convert.ToInt32(stats["PendingAppointments"]),
                TodayAppointments   = Convert.ToInt32(stats["TodayAppointments"]),
                RecentAppointments  = recentRows.Select(r => new Appointment
                {
                    AppointmentId   = Convert.ToInt32(r["AppointmentId"]),
                    PatientId       = Convert.ToInt32(r["PatientId"]),
                    DoctorId        = Convert.ToInt32(r["DoctorId"]),
                    PatientName     = r["PatientName"].ToString()!,
                    PatientEmail    = r["PatientEmail"].ToString()!,
                    DoctorName      = r["DoctorName"].ToString()!,
                    Specialty       = r["Specialty"].ToString()!,
                    ConsultationFee = Convert.ToDecimal(r["ConsultationFee"]),
                    AppointmentDate = Convert.ToDateTime(r["AppointmentDate"]),
                    AppointmentTime = r["AppointmentTime"].ToString()!,
                    ReasonForVisit  = r["ReasonForVisit"]?.ToString(),
                    Status          = r["Status"].ToString()!,
                    IsUrgent        = Convert.ToBoolean(r["IsUrgent"]),
                    CreatedAt       = Convert.ToDateTime(r["CreatedAt"]),
                }).ToList(),
                AppointmentsBySpecialty = specialtyRows.ToDictionary(
                    r => r["Name"].ToString()!,
                    r => Convert.ToInt32(r["Total"]))
            };

            return View(model);
        }

        public async Task<IActionResult> Appointments(string? search, string? status)
        {
            var where      = "WHERE 1=1";
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrEmpty(search))
            {
                where += " AND (u.FullName LIKE @Search OR d.FullName LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            if (!string.IsNullOrEmpty(status))
            {
                where += " AND a.Status = @Status";
                parameters.Add(new SqlParameter("@Status", status));
            }

            var rows = await _db.QueryAsync(
                $@"SELECT a.AppointmentId, a.PatientId, a.DoctorId,
                   u.FullName AS PatientName, u.Email AS PatientEmail,
                   d.FullName AS DoctorName, s.Name AS Specialty,
                   d.ConsultationFee, a.AppointmentDate, a.AppointmentTime,
                   a.ReasonForVisit, a.Status, a.IsUrgent, a.CreatedAt
                   FROM Appointments a
                   JOIN Users u ON a.PatientId = u.UserId
                   JOIN Doctors d ON a.DoctorId = d.DoctorId
                   JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                   {where}
                   ORDER BY a.AppointmentDate DESC",
                parameters.ToArray());

            var appointments = rows.Select(r => new Appointment
            {
                AppointmentId   = Convert.ToInt32(r["AppointmentId"]),
                PatientId       = Convert.ToInt32(r["PatientId"]),
                DoctorId        = Convert.ToInt32(r["DoctorId"]),
                PatientName     = r["PatientName"].ToString()!,
                PatientEmail    = r["PatientEmail"].ToString()!,
                DoctorName      = r["DoctorName"].ToString()!,
                Specialty       = r["Specialty"].ToString()!,
                ConsultationFee = Convert.ToDecimal(r["ConsultationFee"]),
                AppointmentDate = Convert.ToDateTime(r["AppointmentDate"]),
                AppointmentTime = r["AppointmentTime"].ToString()!,
                ReasonForVisit  = r["ReasonForVisit"]?.ToString(),
                Status          = r["Status"].ToString()!,
                IsUrgent        = Convert.ToBoolean(r["IsUrgent"]),
                CreatedAt       = Convert.ToDateTime(r["CreatedAt"]),
            }).ToList();

            ViewBag.SearchTerm   = search;
            ViewBag.StatusFilter = status;
            ViewBag.TotalCount   = appointments.Count;
            return View(appointments);
        }

        public async Task<IActionResult> Patients(string? search)
        {
            var where      = "WHERE Role = 'Patient'";
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrEmpty(search))
            {
                where += " AND (FullName LIKE @Search OR Email LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            var rows = await _db.QueryAsync(
                $"SELECT * FROM Users {where} ORDER BY CreatedAt DESC",
                parameters.ToArray());

            var patients = rows.Select(r => new User
            {
                UserId      = Convert.ToInt32(r["UserId"]),
                FullName    = r["FullName"].ToString()!,
                Email       = r["Email"].ToString()!,
                Phone       = r["Phone"]?.ToString(),
                DateOfBirth = r["DateOfBirth"] as DateTime?,
                Gender      = r["Gender"]?.ToString(),
                IsActive    = Convert.ToBoolean(r["IsActive"]),
                CreatedAt   = Convert.ToDateTime(r["CreatedAt"]),
            }).ToList();

            ViewBag.SearchTerm = search;
            ViewBag.TotalCount = patients.Count;
            return View(patients);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePatient(int id)
        {
            await _db.ExecuteNonQueryAsync(
                "UPDATE Users SET IsActive = ~IsActive WHERE UserId = @Id",
                new SqlParameter("@Id", id));

            TempData["Success"] = "Patient status updated.";
            return RedirectToAction("Patients");
        }
    }
}