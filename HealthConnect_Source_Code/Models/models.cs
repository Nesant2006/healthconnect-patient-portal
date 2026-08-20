using System.ComponentModel.DataAnnotations;

namespace HealthConnect.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string? Phone { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? EmergencyContact { get; set; }
        public string? MedicalNotes { get; set; }
        public string Role { get; set; } = "Patient";
        public bool IsActive { get; set; } = true;
        public string? ProfileImagePath { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";

        [Phone(ErrorMessage = "Invalid phone number")]
        public string? Phone { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }
    }

    public class Doctor
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = "";
        public string Specialty { get; set; } = "";
        public string? Qualifications { get; set; }
        public int ExperienceYears { get; set; }
        public decimal ConsultationFee { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public string? Languages { get; set; }
        public string? ProfileImagePath { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class Appointment
    {
        public int AppointmentId { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public string PatientName { get; set; } = "";
        public string PatientEmail { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string Specialty { get; set; } = "";
        public decimal ConsultationFee { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string AppointmentTime { get; set; } = "";
        public string? ReasonForVisit { get; set; }
        public string Status { get; set; } = "Pending";
        public bool IsUrgent { get; set; }
        public string? DoctorNotes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class AppointmentCreateViewModel
    {
        [Required(ErrorMessage = "Please select a doctor")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Please select a date")]
        public DateTime AppointmentDate { get; set; }

        [Required(ErrorMessage = "Please select a time")]
        public string AppointmentTime { get; set; } = "";

        [StringLength(1000)]
        public string? ReasonForVisit { get; set; }

        public bool IsUrgent { get; set; }

        public List<Doctor>? Doctors { get; set; }

        public List<string> AvailableTimeSlots { get; set; } = GenerateTimeSlots();

        private static List<string> GenerateTimeSlots()
        {
            var slots = new List<string>();
            var start = new TimeSpan(8, 0, 0);
            var end = new TimeSpan(17, 30, 0);
            while (start <= end)
            {
                slots.Add(DateTime.Today.Add(start).ToString("HH:mm"));
                start = start.Add(TimeSpan.FromMinutes(30));
            }
            return slots;
        }
    }

    public class ProfileEditViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100)]
        public string FullName { get; set; } = "";

        [Phone]
        public string? Phone { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? EmergencyContact { get; set; }

        [StringLength(500)]
        public string? MedicalNotes { get; set; }

        public IFormFile? ProfileImage { get; set; }

        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string? ConfirmNewPassword { get; set; }
    }

    public class DashboardViewModel
    {
        public int TotalAppointments { get; set; }
        public int PendingAppointments { get; set; }
        public int ConfirmedAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CancelledAppointments { get; set; }
        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public List<Appointment> PastAppointments { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();
    }

    public class AdminDashboardViewModel
    {
        public int TotalPatients { get; set; }
        public int TotalAppointments { get; set; }
        public int PendingAppointments { get; set; }
        public int TodayAppointments { get; set; }
        public List<Appointment> RecentAppointments { get; set; } = new();
        public Dictionary<string, int> AppointmentsBySpecialty { get; set; } = new();
    }

    public class Notification
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public int StatusCode { get; set; }
    }
}