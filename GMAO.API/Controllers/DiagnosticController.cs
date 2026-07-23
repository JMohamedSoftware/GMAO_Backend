using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GMAO.Infrastructure.Data;

namespace GMAO.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class DiagnosticController : ControllerBase
{
    private readonly GmaoDbContext _db;

    public DiagnosticController(GmaoDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult GetStatus()
    {
        try
        {
            var pending = _db.Database.GetPendingMigrations().ToList();
            var applied = _db.Database.GetAppliedMigrations().ToList();
            var users = _db.Users.Select(u => new { u.Id, u.Email, u.Nom }).ToList();
            var societes = _db.Societes.Select(s => new { s.Id, s.Nom }).ToList();

            return Ok(new {
                Success = true,
                PendingMigrations = pending,
                AppliedMigrations = applied,
                Users = users,
                Societes = societes
            });
        }
        catch (Exception ex)
        {
            return Ok(new { Success = false, Error = ex.Message, Stack = ex.StackTrace });
        }
    }
}
