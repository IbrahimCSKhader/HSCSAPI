using HSCSAPI.Data;
using HSCSAPI.Extensions;
using HSCSAPI.Middleware;
using HSCSAPI.Models.Identity;
using HSCSAPI.Services.Appointments;
using HSCSAPI.Services.AuthorizedMembers;
using HSCSAPI.Services.Auth;
using HSCSAPI.Services.Clinics;
using HSCSAPI.Services.Common;
using HSCSAPI.Services.Doctors;
using HSCSAPI.Services.Email;
using HSCSAPI.Services.Identity;
using HSCSAPI.Services.LaboratoryTechnologists;
using HSCSAPI.Services.Patients;
using HSCSAPI.Services.RadiologyTechnologists;
using HSCSAPI.Services.Secretaries;
using Microsoft.AspNetCore.Identity;
using HSCSAPI.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace HSCSAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            builder.Services.AddControllers();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
            builder.Services.AddEndpointsApiExplorer();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddDbContext<AppDbContext>(options =>
     options.UseSqlServer(
         builder.Configuration.GetConnectionString("DefaultConnection"),
             sqlOptions =>
         {
             sqlOptions.EnableRetryOnFailure(
                 maxRetryCount: 5,
                 maxRetryDelay: TimeSpan.FromSeconds(10),
                 errorNumbersToAdd: null);
         }));
            builder.Services
                .AddIdentityCore<User>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedEmail = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                })
                .AddRoles<Role>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();
            builder.Services.AddScoped<IPasswordHasher<User>, LegacyCompatiblePasswordHasher>();

            builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
            builder.Services.Configure<SuperAdminSeedSettings>(builder.Configuration.GetSection("SuperAdminSeed"));

            var jwtSecret = builder.Configuration["JwtSettings:SecretKey"] ?? string.Empty;
            var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
            var jwtAudience = builder.Configuration["JwtSettings:Audience"];

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtIssuer,
                        ValidAudience = jwtAudience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                    };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<UserIdGeneratorService>();
            builder.Services.AddScoped<IAppointmentsService, AppointmentsService>();
            builder.Services.AddScoped<IAuthorizedMembersService, AuthorizedMembersService>();
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IClinicsService, ClinicsService>();
            builder.Services.AddScoped<IDoctorsService, DoctorsService>();
            builder.Services.AddScoped<ILaboratoryTechnologistsService, LaboratoryTechnologistsService>();
            builder.Services.AddScoped<IPatientsService, PatientsService>();
            builder.Services.AddScoped<IRadiologyTechnologistsService, RadiologyTechnologistsService>();
            builder.Services.AddScoped<ISecretariesService, SecretariesService>();
            builder.Services.AddScoped<IdentitySeedService>();
            builder.Services.AddScoped<IServiceExceptionHandler, ServiceExceptionHandler>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.MapOpenApi();
            app.MapScalarApiReference();
            app.MapGet("/", () => Results.Redirect("/scalar/v1"));

            await app.ApplyMigrationsAndSeedAsync();

            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            await app.RunAsync();
        }
    }
}
