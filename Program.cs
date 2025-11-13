using Microsoft.EntityFrameworkCore;
using BookQuoteAPI.Data;
using BookQuoteAPI.Models;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
        policy => policy.AllowAnyOrigin()
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
app.Run();

