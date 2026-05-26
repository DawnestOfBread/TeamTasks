using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TeamTasks.Application.Auth;
using TeamTasks.Application.Common;
using TeamTasks.Application.DTOs;
using TeamTasks.Domain;
using TeamTasks.Infrastructure;
using TeamTasks.Infrastructure.Security;
using RegisterRequest = TeamTasks.Application.Auth.RegisterRequest;

namespace TeamTasks.Api.Controllers;

[ApiController]
[EnableRateLimiting("AuthPolicy")]
[Route("api/[controller]")]
public class AuthController(IPasswordService passwordService, IJwtService jwtService, AppDbContext context, IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    [EndpointSummary("Creates and logs in a user")]
    [EndpointDescription("Creates an account and appends a HttpOnly session JWT cookie.")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
       await using var transaction = await context.Database.BeginTransactionAsync();
       try 
       {
          string passwordHash = passwordService.HashPassword(request.Password);
          var user = new User 
          {
             Id = Guid.NewGuid(),
             Email = request.Email,
             Name = request.Username,
             PasswordHash = passwordHash
          };
          context.Users.Add(user);

          await context.SaveChangesAsync();
          await transaction.CommitAsync();
          
          CreateToken(user, Guid.Empty);

          return Ok(new { id = user.Id, email = user.Email });
       }
       catch (Exception)
       {
          await transaction.RollbackAsync();
          return BadRequest("Registration failed");
       }
    }
    
    [HttpPost("login")]
    [EndpointSummary("Authenticates a user")]
    [EndpointDescription("Verifies credentials and appends a HttpOnly session JWT cookie.")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
       // IgnoreQueryFilters is required here because the tenant context is not yet resolved
       var user = await context.Users
          .IgnoreQueryFilters()
          .Include(user => user.Organizations)
          .FirstOrDefaultAsync(u => u.Email == request.Email);

       if (user == null || !passwordService.VerifyPassword(request.Password, user.PasswordHash))
          return Unauthorized("Invalid email or password.");

       var orgId = Guid.Empty;
       if (user.Organizations.Count > 0)
          orgId = user.Organizations.First().Id;
          
       CreateToken(user, orgId);

       return Ok(new { id = user.Id, email = user.Email });
    }
    
    [HttpGet("login-google")]
    public IActionResult LoginGoogle()
    {
       var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google-callback" };
       return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }
    
    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        string frontendUrl = configuration["Frontend:BaseUrl"] ?? "/";
       
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal == null)
            return Redirect(frontendUrl + "login?error=google_auth_failed");
        
        string? email = result.Principal.FindFirstValue(ClaimTypes.Email);
        string name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? "Google User";

        if (string.IsNullOrEmpty(email))
            return Redirect(frontendUrl + "login?error=invalid_email");

        var targetOrgId = Guid.Empty;
        try
        {
            var user = await context.Users
               .IgnoreQueryFilters()
               .Include(u => u.Organizations)
               .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        Name = name,
                        Provider = "Google",
                        PasswordHash = string.Empty
                    };
                    
                    context.Users.Add(user);
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    if (context.Database.CurrentTransaction != null) 
                       await transaction.RollbackAsync();
                    throw;
                }
            }
            else
            {
               targetOrgId = user.Organizations.FirstOrDefault()?.Id ?? Guid.Empty;
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            CreateToken(user, targetOrgId);
            
            return Redirect(frontendUrl);
        }
        catch (Exception)
        {
            return Redirect(frontendUrl + "login?error=registration_processing_failed");
        }
    }
    
    [HttpPost("switch-org/{id}")]
    [Authorize]
    public async Task<IActionResult> SwitchOrg(Guid id)
    {
       var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
       
       var user = await context.Users
          .IgnoreQueryFilters()
          .FirstOrDefaultAsync(u => u.Id == userId && u.Organizations.Any(o => o.Id == id));

       if (user == null) return Forbid();
       
       CreateToken(user, id);
       return Ok();
    }
    
    private void CreateToken(User user, Guid orgId)
    {
       string token = jwtService.GenerateToken(user.Id, orgId, user.Email);
       AppendCookie(token);
    }

    private void AppendCookie(string newToken)
    {
       Response.Cookies.Append("X-Auth-Token", newToken, new CookieOptions
       {
          HttpOnly = true,
          Secure = true,
          SameSite = SameSiteMode.Lax,
          Expires = DateTime.UtcNow.AddDays(7)
       });
    }
}