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
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Configure in-memory database for development/testing
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("BookQuoteDB"));

// Configure CORS to allow requests from Angular frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy
        .WithOrigins(
            "http://localhost:4200",
            "https://spontaneous-selkie-760b6f.netlify.app/"
        )
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// Configure JWT authentication
// Validates token signature, expiration, and claims on protected endpoints
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

// Seed initial data for demo purposes
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Only seed if database is empty
    if (!context.Quotes.Any())
    {
        context.Quotes.AddRange(
            new Quote { QuoteText = "The only thing we have to fear is fear itself.", Author = "Franklin D. Roosevelt " },
            new Quote { QuoteText = "I fear not the man who has practiced 10,000 kicks once, but I fear the man who has practiced one kick 10,000 times", Author = "Bruce Lee" },
            new Quote { QuoteText = "Code is like humor. When you have to explain it, it's bad.", Author = "Cory House" },
            new Quote { QuoteText = "Yesterday is history, tomorrow is a mystery, today is a gift of God, which is why we call it the present.", Author = "Bil Keane " },
            new Quote { QuoteText = "The journey of a thousand miles begins with one step.", Author = "Lao Tzu " }
        );
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseAuthentication();// Enable JWT authentication middleware
app.UseAuthorization();// Enable authorization checks
app.MapControllers();

// GET all books - Protected endpoint (requires valid JWT token)
app.MapGet("/api/books", async (AppDbContext context) =>
{
    return await context.Books.ToListAsync();
}).RequireAuthorization();

// POST create new book - Protected endpoint
app.MapPost("/api/books", async (AppDbContext context, Book book) =>
{
    context.Books.Add(book);
    await context.SaveChangesAsync();
    return Results.Created($"/api/books/{book.Id}", book);
}).RequireAuthorization();

// PUT update existing book - Protected endpoint
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

// DELETE book - Protected endpoint
app.MapDelete("/api/books/{id}", async (AppDbContext context, int id) =>
{
    var book = await context.Books.FindAsync(id);
    if (book == null) return Results.NotFound();

    context.Books.Remove(book);
    await context.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// GET all quotes - Protected endpoint (requires valid JWT token)
app.MapGet("/api/quotes", async (AppDbContext context) =>
{
    return await context.Quotes.ToListAsync();
}).RequireAuthorization();

// POST create new quote - Protected endpoint
app.MapPost("/api/quotes", async (AppDbContext context, Quote quote) =>
{

    context.Quotes.Add(quote);
    await context.SaveChangesAsync();
    return Results.Created($"/api/quotes/{quote.Id}", quote);
}).RequireAuthorization();

// PUT update existing quote - Protected endpoint
app.MapPut("/api/quotes/{id}", async (AppDbContext context, int id, Quote updatedQuote) =>
{
    var quote = await context.Quotes.FindAsync(id);
    if (quote == null) return Results.NotFound();

    quote.QuoteText = updatedQuote.QuoteText;
    quote.Author = updatedQuote.Author;

    await context.SaveChangesAsync();
    return Results.Ok(quote);
}).RequireAuthorization();

// DELETE quote - Protected endpoint
app.MapDelete("/api/quotes/{id}", async (AppDbContext context, int id) =>
{
    var quote = await context.Quotes.FindAsync(id);
    if (quote == null) return Results.NotFound();

    context.Quotes.Remove(quote);
    await context.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// POST register new user - Public endpoint (no authentication required)
app.MapPost("/auth/register", async (AppDbContext context, [FromBody] RegisterDto registerDto, IConfiguration config) =>
{   

    // Check if user already exists
    if (await context.Users.AnyAsync(u => u.Email == registerDto.Email))
    {
        return Results.BadRequest(new { message = "Användare finns redan" });
    }

    // Hash password before storing 
    string passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

    // Create new user with hashed password
    var user = new User
    {
        Username = registerDto.Username,
        Email = registerDto.Email,
        PasswordHash = passwordHash
    };

    // Save to database
    context.Users.Add(user);
    await context.SaveChangesAsync();

    // Generate JWT token for automatic login after registration
    string token = CreateToken(user, config);

    return Results.Ok(new { token, email = user.Email });
});

// POST login user - Public endpoint (no authentication required)
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

// Creates a JWT token for authenticated user
static string CreateToken(User user, IConfiguration config)
{   
    // Step 1: Create claims (user data stored in token)
    List<Claim> claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Name, user.Username)
    };

    // Step 2: Get secret key from configuration
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
        config.GetSection("AppSettings:Token").Value!));

    // Step 3: Create signing credentials using HMAC SHA512
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

    // Step 4: Build JWT token with claims and 1-day expiration
    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.Now.AddDays(1),
        signingCredentials: creds
    );

    // Step 5: Convert token to string format
    return new JwtSecurityTokenHandler().WriteToken(token);
}

