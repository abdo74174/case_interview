using CaseInterview.Application.Common.Interfaces;
using CaseInterview.Application.Features.Auth.DTOs;
using CaseInterview.Application.Features.Auth.Services;
using CaseInterview.Domain.Entities;
using CaseInterview.Domain.Exceptions;
using CaseInterview.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CaseInterview.UnitTests;

public class AuthServiceTests
{
    private readonly CaseInterviewDbContext _dbContext;
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<CaseInterviewDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CaseInterviewDbContext(options);

        _passwordHasherMock.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns<string>(p => $"hashed_{p}");
        _passwordHasherMock.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((p, h) => h == $"hashed_{p}");

        _jwtTokenServiceMock.Setup(j => j.GenerateToken(It.IsAny<User>()))
            .Returns("test-jwt-token");

        _authService = new AuthService(_dbContext, _passwordHasherMock.Object, _jwtTokenServiceMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUserAndReturnsToken()
    {
        var request = new RegisterRequest("architect@systemdesign.com", "Password123!", "Senior Architect");

        var response = await _authService.RegisterAsync(request);

        response.Should().NotBeNull();
        response.Email.Should().Be("architect@systemdesign.com");
        response.FullName.Should().Be("Senior Architect");
        response.Token.Should().Be("test-jwt-token");

        var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "architect@systemdesign.com");
        userInDb.Should().NotBeNull();
        userInDb!.PasswordHash.Should().Be("hashed_Password123!");
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsValidationException()
    {
        var existingUser = new User
        {
            Email = "duplicate@test.com",
            FullName = "Existing User",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var request = new RegisterRequest("duplicate@test.com", "Password123!", "Another Name");

        var act = async () => await _authService.RegisterAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        var user = new User
        {
            Email = "candidate@test.com",
            FullName = "Candidate One",
            PasswordHash = "hashed_CorrectPass"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest("candidate@test.com", "CorrectPass");

        var response = await _authService.LoginAsync(request);

        response.Should().NotBeNull();
        response.Email.Should().Be("candidate@test.com");
        response.Token.Should().Be("test-jwt-token");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsDomainException()
    {
        var user = new User
        {
            Email = "candidate@test.com",
            FullName = "Candidate One",
            PasswordHash = "hashed_CorrectPass"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest("candidate@test.com", "WrongPassword");

        var act = async () => await _authService.LoginAsync(request);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Invalid email or password.");
    }
}
