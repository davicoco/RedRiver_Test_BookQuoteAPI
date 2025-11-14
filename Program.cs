using Microsoft.EntityFrameworkCore;
using BookQuoteAPI.Data;
using BookQuoteAPI.Models;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("BookQuoteDB"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy
        .WithOrigins(
            "http://localhost:4200",
            "https://vermillion-mooncake-01ea7b.netlify.app/"
        )
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(Options =>
    {
        Options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(builder.Configuration.GetSection("AppSettings:Token").Value!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/books", async (AppDbContext context) =>
{
    return await context.Books.ToListAsync();
}).RequireAuthorization();

app.MapPost("/api/books", async (AppDbContext context, Book book) =>
{
    context.Books.Add(book);
    await context.SaveChangesAsync();
    return Results.Created($"/api/books/{book.Id}", book);
}).RequireAuthorization();

app.MapPut("/api/books/{id}", async (AppDbContext context, int id, Book updatedBook) =>
{
    var book = await context.Books.FindAsync(id);
    if (book == null) return Results.NotFound();

    book.Title = updatedBook.Title;
    book.Author = updatedBook.Author;
    book.Genre = updatedBook.Genre;
    book.PublicationDate = updatedBook.PublicationDate;

    await context.SaveChangesAsync();
    return Results.Ok(book);
}).RequireAuthorization();

app.MapDelete("/api/books/{id}", async (AppDbContext context, int id) =>
{
    var book = await context.Books.FindAsync(id);
    if (book == null) return Results.NotFound();

    context.Books.Remove(book);
    await context.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/quotes", async (AppDbContext context) =>
{
    return await context.Quotes.ToListAsync();
}).RequireAuthorization();

app.MapPost("/api/quotes", async (AppDbContext context, Quote quote) =>
{

    context.Quotes.Add(quote);
    await context.SaveChangesAsync();
    return Results.Created($"/api/quotes/{quote.Id}", quote);
}).RequireAuthorization();

app.MapPut("/api/quotes/{id}", async (AppDbContext context, int id, Quote updatedQuote) =>
{
    var quote = await context.Quotes.FindAsync(id);
    if (quote == null) return Results.NotFound();

    quote.QuoteText = updatedQuote.QuoteText;
    quote.Author = updatedQuote.Author;

    await context.SaveChangesAsync();
    return Results.Ok(quote);
}).RequireAuthorization();

app.MapDelete("/api/quotes/{id}", async (AppDbContext context, int id) =>
{
    var quote = await context.Quotes.FindAsync(id);
    if (quote == null) return Results.NotFound();

    context.Quotes.Remove(quote);
    await context.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

app.MapPost("/auth/register", async (AppDbContext context, [FromBody] RegisterDto registerDto, IConfiguration config) =>
{
    if (await context.Users.AnyAsync(u => u.Email == registerDto.Email))
    {
        return Results.BadRequest(new { message = "Användare finns redan" });
    }

    //skapa pw hash
    string passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

    var user = new User
    {
        Username = registerDto.Username,
        Email = registerDto.Email,
        PasswordHash = passwordHash
    };

    context.Users.Add(user);
    await context.SaveChangesAsync();

    //generera token
    string token = CreateToken(user, config);

    return Results.Ok(new { token, email = user.Email });
});

app.MapPost("/auth/login", async (AppDbContext context, [FromBody] LoginDto loginDto, IConfiguration config) =>
{

    var user = await context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

    if (user == null)
    {
        return Results.BadRequest(new { message = "Ogiltiga uppgifter!" });
    }

    if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
    {
        return Results.BadRequest(new { message = "Ogiltiga uppgifter" });
    }

    string token = CreateToken(user, config);

    return Results.Ok(new { token, email = user.Email });
});

app.Run();

static string CreateToken(User user, IConfiguration config)
{
    List<Claim> claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.Username)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
        config.GetSection("AppSettings:Token").Value!));

    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.Now.AddDays(1),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

