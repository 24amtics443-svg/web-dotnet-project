using HealthcareManagementSystem.Data;
using HealthcareManagementSystem.Models;
using HealthcareManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthcareManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // =========================================================
        // ADMIN INDEX
        // =========================================================

        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }


        // =========================================================
        // ADMIN DASHBOARD
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Dashboard(string period = "all")
        {
            // -----------------------------------------------------
            // Current Admin
            // -----------------------------------------------------

            var currentUser = await _userManager.GetUserAsync(User);

            var adminName = currentUser?.FullName;

            if (string.IsNullOrWhiteSpace(adminName))
            {
                adminName = "Administrator";
            }


            // -----------------------------------------------------
            // Validate Period
            // -----------------------------------------------------

            period = period?.ToLowerInvariant() ?? "all";

            if (period != "all" &&
                period != "week" &&
                period != "month" &&
                period != "year")
            {
                period = "all";
            }


            // -----------------------------------------------------
            // Date Range
            // -----------------------------------------------------

            var today = DateTime.Today;

            var tomorrow = today.AddDays(1);

            DateTime? periodStart = null;

            DateTime? periodEnd = null;


            switch (period)
            {
                case "week":

                    int difference =
                        (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;

                    periodStart =
                        today.AddDays(-difference);

                    periodEnd =
                        periodStart.Value.AddDays(7);

                    break;


                case "month":

                    periodStart =
                        new DateTime(
                            today.Year,
                            today.Month,
                            1
                        );

                    periodEnd =
                        periodStart.Value.AddMonths(1);

                    break;


                case "year":

                    periodStart =
                        new DateTime(
                            today.Year,
                            1,
                            1
                        );

                    periodEnd =
                        periodStart.Value.AddYears(1);

                    break;


                default:

                    periodStart = null;
                    periodEnd = null;

                    break;
            }


            // -----------------------------------------------------
            // Appointment Query
            // -----------------------------------------------------

            var appointmentQuery =
                _context.Appointments.AsQueryable();


            if (periodStart.HasValue &&
                periodEnd.HasValue)
            {
                appointmentQuery =
                    appointmentQuery.Where(a =>
                        a.AppointmentDate >= periodStart.Value &&
                        a.AppointmentDate < periodEnd.Value
                    );
            }


            // -----------------------------------------------------
            // Main Statistics
            // -----------------------------------------------------

            var totalUsers =
                await _userManager.Users.CountAsync();


            var totalPatients =
                await _context.Patients.CountAsync();


            var totalDoctors =
                await _context.Doctors.CountAsync();


            // -----------------------------------------------------
            // Appointment Statistics
            // -----------------------------------------------------

            var totalAppointments =
                await appointmentQuery.CountAsync();


            var todaysAppointments =
                await _context.Appointments.CountAsync(a =>
                    a.AppointmentDate >= today &&
                    a.AppointmentDate < tomorrow
                );


            var pendingAppointments =
                await appointmentQuery.CountAsync(a =>
                    a.Status != null &&
                    a.Status.ToLower() == "pending"
                );


            var completedAppointments =
                await appointmentQuery.CountAsync(a =>
                    a.Status != null &&
                    a.Status.ToLower() == "completed"
                );


            var cancelledAppointments =
                await appointmentQuery.CountAsync(a =>
                    a.Status != null &&
                    a.Status.ToLower() == "cancelled"
                );


            var activeAppointments =
                await appointmentQuery.CountAsync(a =>
                    a.Status != null &&
                    a.Status.ToLower() != "cancelled" &&
                    a.Status.ToLower() != "completed"
                );


            // -----------------------------------------------------
            // Clinical Statistics
            // -----------------------------------------------------

            var totalMedicalRecords =
                await _context.MedicalRecords.CountAsync();


            var totalPrescriptions =
                await _context.Prescriptions.CountAsync();


            var totalLabReports =
                await _context.LabReports.CountAsync();


            // Existing model does not currently provide
            // a confirmed Pending Lab Report property.
            var pendingReports = 0;


            // -----------------------------------------------------
            // Billing / Revenue
            // -----------------------------------------------------

            var totalBillingAmount =
                await _context.Bills
                    .Select(b => (decimal?)b.TotalAmount)
                    .SumAsync() ?? 0m;


            var totalRevenue =
                totalBillingAmount;


            // -----------------------------------------------------
            // Recent Appointments
            // -----------------------------------------------------

            var recentAppointments =
                await appointmentQuery
                    .Include(a => a.Patient)
                    .Include(a => a.Doctor)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ThenByDescending(a => a.AppointmentTime)
                    .Take(8)
                    .ToListAsync();


            // -----------------------------------------------------
            // Recent Doctors
            // -----------------------------------------------------

            var recentDoctors =
                await _context.Doctors
                    .Include(d => d.Department)
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(5)
                    .ToListAsync();


            // -----------------------------------------------------
            // Recent Patients
            // -----------------------------------------------------

            var recentPatients =
                await _context.Patients
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(5)
                    .ToListAsync();


            // -----------------------------------------------------
            // Dashboard ViewModel
            // -----------------------------------------------------

            var model = new AdminDashboardViewModel
            {
                TotalPatients = totalPatients,

                TotalDoctors = totalDoctors,

                TotalUsers = totalUsers,

                TotalAppointments = totalAppointments,

                TodaysAppointments = todaysAppointments,

                ActiveAppointments = activeAppointments,

                PendingAppointments = pendingAppointments,

                CompletedAppointments = completedAppointments,

                CancelledAppointments = cancelledAppointments,

                TotalMedicalRecords = totalMedicalRecords,

                TotalPrescriptions = totalPrescriptions,

                TotalLabReports = totalLabReports,

                PendingReports = pendingReports,

                TotalRevenue = totalRevenue,

                TotalBillingAmount = totalBillingAmount,

                RecentAppointments = recentAppointments,

                RecentDoctors = recentDoctors,

                RecentPatients = recentPatients,

                AdminName = adminName
            };


            // -----------------------------------------------------
            // Selected Period
            // -----------------------------------------------------

            ViewBag.SelectedPeriod = period;

            ViewData["Title"] = "Admin Dashboard";


            return View(model);
        }


        // =========================================================
        // USERS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var userList =
                new List<AdminUserViewModel>();


            foreach (var user in users)
            {
                var roles =
                    await _userManager.GetRolesAsync(user);


                userList.Add(
                    new AdminUserViewModel
                    {
                        Id = user.Id,

                        FullName =
                            string.IsNullOrWhiteSpace(user.FullName)
                                ? "User"
                                : user.FullName,

                        Email = user.Email ?? "",

                        PhoneNumber =
                            user.PhoneNumber ?? "",

                        Role =
                            roles.FirstOrDefault()
                            ?? "User",

                        IsActive =
                            !user.LockoutEnd.HasValue ||
                            user.LockoutEnd.Value <=
                                DateTimeOffset.UtcNow
                    }
                );
            }


            ViewData["Title"] = "User Management";

            return View(userList);
        }


        // =========================================================
        // DELETE USER
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction(nameof(Users));
            }


            var user =
                await _userManager.FindByIdAsync(id);


            if (user == null)
            {
                return RedirectToAction(nameof(Users));
            }


            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] =
                    "Admin account cannot be deleted.";

                return RedirectToAction(nameof(Users));
            }


            var result =
                await _userManager.DeleteAsync(user);


            if (result.Succeeded)
            {
                TempData["Success"] =
                    "User deleted successfully.";
            }
            else
            {
                TempData["Error"] =
                    string.Join(
                        " ",
                        result.Errors.Select(e => e.Description)
                    );
            }


            return RedirectToAction(nameof(Users));
        }


        // =========================================================
        // TOGGLE USER STATUS
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction(nameof(Users));
            }


            var user =
                await _userManager.FindByIdAsync(id);


            if (user == null)
            {
                return RedirectToAction(nameof(Users));
            }


            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] =
                    "Admin account cannot be blocked.";

                return RedirectToAction(nameof(Users));
            }


            if (user.LockoutEnd.HasValue &&
                user.LockoutEnd.Value >
                DateTimeOffset.UtcNow)
            {
                user.LockoutEnd = null;

                user.LockoutEnabled = true;

                TempData["Success"] =
                    "User activated successfully.";
            }
            else
            {
                user.LockoutEnabled = true;

                user.LockoutEnd =
                    DateTimeOffset.UtcNow.AddYears(100);

                TempData["Success"] =
                    "User blocked successfully.";
            }


            await _userManager.UpdateAsync(user);


            return RedirectToAction(nameof(Users));
        }


        // =========================================================
        // OTHER ADMIN PAGES
        // =========================================================

        [HttpGet]
        public IActionResult Doctors()
        {
            ViewData["Title"] =
                "Doctor Management";

            return View();
        }


        [HttpGet]
        public IActionResult Patients()
        {
            ViewData["Title"] =
                "Patient Management";

            return View();
        }


        [HttpGet]
        public IActionResult Appointments()
        {
            ViewData["Title"] =
                "Appointment Management";

            return View();
        }


        [HttpGet]
        public IActionResult Records()
        {
            ViewData["Title"] =
                "Medical Records";

            return View();
        }


        [HttpGet]
        public IActionResult Billing()
        {
            ViewData["Title"] =
                "Billing Management";

            return View();
        }


        [HttpGet]
        public IActionResult Reports()
        {
            ViewData["Title"] =
                "Reports & Analytics";

            return View();
        }


        [HttpGet]
        public IActionResult Roles()
        {
            ViewData["Title"] =
                "Roles & Permissions";

            return View();
        }


        [HttpGet]
        public IActionResult Settings()
        {
            ViewData["Title"] =
                "System Settings";

            return View();
        }
    }
}