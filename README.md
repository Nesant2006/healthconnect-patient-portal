# HealthConnect — Patient Portal (ASP.NET Core MVC)

A full-stack healthcare patient portal built with ASP.NET Core MVC, allowing patients to register, book appointments, and manage their care, with a separate admin panel for clinic staff.

**Unit:** ITEC 323 — Web and Mobile Application Development, Australian Catholic University
**Author:** Nishant Shrestha

## Overview

HealthConnect is a role-based web application simulating a real clinic's patient portal:

- **Patients** can register, log in, search for doctors by specialty, book appointments, and view/edit/cancel their bookings from a personal dashboard
- **Admins** log in separately to confirm/cancel appointments, manage patient accounts, and view system-wide statistics via a Chart.js dashboard
- Fully responsive (mobile-first) design with a dark mode toggle, built with Tailwind CSS

## Tech Stack

- **Backend:** ASP.NET Core MVC (C#)
- **Database:** SQL Server, accessed via ADO.NET with parameterised queries
- **Frontend:** Razor views, Tailwind CSS, Chart.js
- **Auth:** ASP.NET Cookie Authentication with BCrypt password hashing

## Security Features

- **SQL Injection protection** — every query uses `SqlParameter`; no raw string concatenation
- **CSRF protection** — anti-forgery tokens validated on every POST
- **Password security** — BCrypt hashing (work factor 11)
- **Session security** — HttpOnly, sliding-expiration cookies
- **Role-based authorisation** — `[Authorize(Roles = "Admin")]` restricts admin routes; unauthorised access returns 403

## Accessibility

Built against WCAG 2.1 guidelines: skip-to-content link, ARIA attributes on interactive elements, 4.5:1 minimum colour contrast, 44×44px touch targets, and visible keyboard focus states.

## Project Structure (MVC)

- `Controllers/` — `AccountController` (auth), `AppointmentController` (booking CRUD), `HomeController` (home, dashboard, doctors, and admin views)
- `Models/` — data models including `Appointment`, `Doctor`, `User`
- `Views/` — Razor views organised by controller
- `Data/` — `DatabaseHelper.cs`, handling parameterised ADO.NET queries
- `wwwroot/` — custom CSS/JS (Tailwind CDN handles most styling)

## Running Locally

1. Requires .NET SDK and SQL Server (Express is sufficient)
2. Create the database using the schema described in the project report (Users, Doctors, Specialties, Appointments, Notifications, AuditLog)
3. Update the connection string in `appsettings.json` to point to your SQL Server instance
4. Run `dotnet restore` then `dotnet run`

## Repository Contents

- Full ASP.NET Core MVC source (Controllers, Models, Views, Data)
- `Nishant_Shrestha_HealthConnect_Report.pdf` — full project report with screenshots, architecture explanation, security discussion, and accessibility audit

> Note: `appsettings.json` in this repo uses a placeholder connection string — no real credentials are committed.

## References

- Microsoft (2024). *Overview of ASP.NET Core MVC*. Microsoft Learn.
- OWASP Foundation (2021). *OWASP Top Ten*.
- Provos, N., & Mazieres, D. (1999). *A Future-Adaptable Password Scheme*. USENIX.
- W3C (2018). *Web Content Accessibility Guidelines (WCAG) 2.1*.
- Tailwind Labs (2024). *Tailwind CSS Documentation*.
