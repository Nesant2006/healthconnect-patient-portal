using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using HealthConnect.Data;
using HealthConnect.Models;

namespace HealthConnect.Controllers
{
    [Authorize]
    public class AppointmentController : Controller
    {
        private readonly DatabaseHelper _db;

        public AppointmentController(DatabaseHelper db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(string? search, string? status, string? sort)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = User.IsInRole("Admin");

            var where = isAdmin ? "WHERE 1=1" : "WHERE a.PatientId = @UserId";
            var parameters = new List<SqlParameter>();
            if (!isAdmin) parameters.Add(new SqlParameter("@UserId", userId));

            if (!string.IsNullOrEmpty(search))
            {
                where += " AND (u.FullName LIKE @Search OR d.FullName LIKE @Search OR a.ReasonForVisit LIKE @Search)";
                parameters.Add(new SqlParameter("@Search", $"%{search}%"));
            }

            if (!string.IsNullOrEmpty(status))
            {
                where += " AND a.Status = @Status";
                parameters.Add(new SqlParameter("@Status", status));
            }

            var orderBy = sort == "oldest" ? "ORDER BY a.AppointmentDate ASC" : "ORDER BY a.AppointmentDate DESC";

            var sql = $@"SELECT a.AppointmentId, a.PatientId, a.DoctorId,
                        u.FullName AS PatientName, u.Email AS PatientEmail,
                        d.FullName AS DoctorName, s.Name AS Specialty,
                        d.ConsultationFee, a.AppointmentDate, a.AppointmentTime,
                        a.ReasonForVisit, a.Status, a.IsUrgent, a.CreatedAt
                        FROM Appointments a
                        JOIN Users u ON a.PatientId = u.UserId
                        JOIN Doctors d ON a.DoctorId = d.DoctorId
                        JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                        {where} {orderBy}";

            var rows = await _db.QueryAsync(sql, parameters.ToArray());
            var appointments = rows.Select(MapAppointment).ToList();

            ViewBag.SearchTerm   = search;
            ViewBag.StatusFilter = status;
            ViewBag.Sort         = sort;
            ViewBag.TotalCount   = appointments.Count;
            return View(appointments);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appt   = await GetAppointmentById(id);
            if (appt == null) return NotFound();
            if (!User.IsInRole("Admin") && appt.PatientId != userId) return Forbid();
            return View(appt);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? doctorId)
        {
            var doctors = await GetDoctors();
            var model   = new AppointmentCreateViewModel { Doctors = doctors };
            if (doctorId.HasValue) model.DoctorId = doctorId.Value;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppointmentCreateViewModel model)
        {
            model.Doctors = await GetDoctors();

            if (!ModelState.IsValid) return View(model);

            // Custom validation
            var validation = AppointmentValidator.Validate(model.AppointmentDate);
            if (!validation.IsValid)
            {
                ModelState.AddModelError("AppointmentDate", validation.ErrorMessage!);
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            try
            {
                // Duplicate check
                var duplicate = await _db.ExecuteScalarAsync<int>(
                    @"SELECT COUNT(1) FROM Appointments
                      WHERE PatientId=@PId AND DoctorId=@DId
                      AND AppointmentDate=@Date AND AppointmentTime=@Time
                      AND Status != 'Cancelled'",
                    new SqlParameter("@PId",  userId),
                    new SqlParameter("@DId",  model.DoctorId),
                    new SqlParameter("@Date", model.AppointmentDate.Date),
                    new SqlParameter("@Time", model.AppointmentTime));

                if (duplicate > 0)
                {
                    ModelState.AddModelError("", "You already have an appointment with this doctor at the same time.");
                    return View(model);
                }

                await _db.ExecuteNonQueryAsync(
                    @"INSERT INTO Appointments (PatientId, DoctorId, AppointmentDate, AppointmentTime, ReasonForVisit, IsUrgent, Status)
                      VALUES (@PId, @DId, @Date, @Time, @Reason, @Urgent, 'Pending')",
                    new SqlParameter("@PId",    userId),
                    new SqlParameter("@DId",    model.DoctorId),
                    new SqlParameter("@Date",   model.AppointmentDate.Date),
                    new SqlParameter("@Time",   model.AppointmentTime),
                    new SqlParameter("@Reason", (object?)model.ReasonForVisit ?? DBNull.Value),
                    new SqlParameter("@Urgent", model.IsUrgent));

                // Create notification
                await _db.ExecuteNonQueryAsync(
                    @"INSERT INTO Notifications (UserId, Title, Message)
                      VALUES (@UserId, 'Appointment Booked', 'Your appointment has been booked and is pending confirmation.')",
                    new SqlParameter("@UserId", userId));

                TempData["Success"] = "Appointment booked successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Booking failed. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appt   = await GetAppointmentById(id);
            if (appt == null) return NotFound();
            if (!User.IsInRole("Admin") && appt.PatientId != userId) return Forbid();
            if (appt.Status == "Completed" || appt.Status == "Cancelled")
            {
                TempData["Error"] = "This appointment cannot be edited.";
                return RedirectToAction("Details", new { id });
            }

            var model = new AppointmentCreateViewModel
            {
                DoctorId        = appt.DoctorId,
                AppointmentDate = appt.AppointmentDate,
                AppointmentTime = appt.AppointmentTime,
                ReasonForVisit  = appt.ReasonForVisit,
                IsUrgent        = appt.IsUrgent,
                Doctors         = await GetDoctors()
            };
            ViewBag.AppointmentId = id;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AppointmentCreateViewModel model)
        {
            model.Doctors = await GetDoctors();
            var userId    = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appt      = await GetAppointmentById(id);
            if (appt == null) return NotFound();
            if (!User.IsInRole("Admin") && appt.PatientId != userId) return Forbid();

            if (!ModelState.IsValid) { ViewBag.AppointmentId = id; return View(model); }

            var validation = AppointmentValidator.Validate(model.AppointmentDate);
            if (!validation.IsValid)
            {
                ModelState.AddModelError("AppointmentDate", validation.ErrorMessage!);
                ViewBag.AppointmentId = id;
                return View(model);
            }

            try
            {
                await _db.ExecuteNonQueryAsync(
                    @"UPDATE Appointments SET DoctorId=@DId, AppointmentDate=@Date,
                      AppointmentTime=@Time, ReasonForVisit=@Reason,
                      IsUrgent=@Urgent, Status='Pending'
                      WHERE AppointmentId=@Id",
                    new SqlParameter("@DId",    model.DoctorId),
                    new SqlParameter("@Date",   model.AppointmentDate.Date),
                    new SqlParameter("@Time",   model.AppointmentTime),
                    new SqlParameter("@Reason", (object?)model.ReasonForVisit ?? DBNull.Value),
                    new SqlParameter("@Urgent", model.IsUrgent),
                    new SqlParameter("@Id",     id));

                TempData["Success"] = "Appointment updated successfully!";
                return RedirectToAction("Details", new { id });
            }
            catch
            {
                ModelState.AddModelError("", "Update failed. Please try again.");
                ViewBag.AppointmentId = id;
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appt   = await GetAppointmentById(id);
            if (appt == null) return NotFound();
            if (!User.IsInRole("Admin") && appt.PatientId != userId) return Forbid();

            await _db.ExecuteNonQueryAsync(
                "UPDATE Appointments SET Status='Cancelled' WHERE AppointmentId=@Id",
                new SqlParameter("@Id", id));

            TempData["Success"] = "Appointment cancelled.";
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            await _db.ExecuteNonQueryAsync(
                "UPDATE Appointments SET Status=@Status WHERE AppointmentId=@Id",
                new SqlParameter("@Status", status),
                new SqlParameter("@Id",     id));

            TempData["Success"] = $"Appointment marked as {status}.";
            return RedirectToAction("Index");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<Appointment?> GetAppointmentById(int id)
        {
            var row = await _db.QuerySingleAsync(
                @"SELECT a.AppointmentId, a.PatientId, a.DoctorId,
                  u.FullName AS PatientName, u.Email AS PatientEmail,
                  d.FullName AS DoctorName, s.Name AS Specialty,
                  d.ConsultationFee, a.AppointmentDate, a.AppointmentTime,
                  a.ReasonForVisit, a.Status, a.IsUrgent, a.DoctorNotes, a.CreatedAt
                  FROM Appointments a
                  JOIN Users u ON a.PatientId = u.UserId
                  JOIN Doctors d ON a.DoctorId = d.DoctorId
                  JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                  WHERE a.AppointmentId = @Id",
                new SqlParameter("@Id", id));

            return row == null ? null : MapAppointment(row);
        }

        private async Task<List<Doctor>> GetDoctors()
        {
            var rows = await _db.QueryAsync(
                @"SELECT d.DoctorId, d.FullName, s.Name AS Specialty,
                  d.ConsultationFee, d.Rating, d.ReviewCount,
                  d.Qualifications, d.ExperienceYears, d.Languages
                  FROM Doctors d
                  JOIN Specialties s ON d.SpecialtyId = s.SpecialtyId
                  WHERE d.IsAvailable = 1
                  ORDER BY s.Name, d.FullName");

            return rows.Select(r => new Doctor
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
        }

        private static Appointment MapAppointment(Dictionary<string, object> r) => new()
        {
            AppointmentId   = Convert.ToInt32(r["AppointmentId"]),
            PatientId       = Convert.ToInt32(r["PatientId"]),
            DoctorId        = Convert.ToInt32(r["DoctorId"]),
            PatientName     = r["PatientName"].ToString()!,
            PatientEmail    = r.ContainsKey("PatientEmail") ? r["PatientEmail"].ToString()! : "",
            DoctorName      = r["DoctorName"].ToString()!,
            Specialty       = r["Specialty"].ToString()!,
            ConsultationFee = Convert.ToDecimal(r["ConsultationFee"]),
            AppointmentDate = Convert.ToDateTime(r["AppointmentDate"]),
            AppointmentTime = r["AppointmentTime"].ToString()!,
            ReasonForVisit  = r["ReasonForVisit"]?.ToString(),
            Status          = r["Status"].ToString()!,
            IsUrgent        = Convert.ToBoolean(r["IsUrgent"]),
            DoctorNotes     = r.ContainsKey("DoctorNotes") ? r["DoctorNotes"]?.ToString() : null,
            CreatedAt       = Convert.ToDateTime(r["CreatedAt"]),
        };
    }

    public static class AppointmentValidator
    {
        public static (bool IsValid, string? ErrorMessage) Validate(DateTime date)
        {
            if (date.Date <= DateTime.Today)
                return (false, "Appointment date must be in the future.");
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                return (false, "Appointments are only available on weekdays.");
            if (date.Date > DateTime.Today.AddDays(90))
                return (false, "Appointments cannot be booked more than 90 days in advance.");
            return (true, null);
        }
    }
}