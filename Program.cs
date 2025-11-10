using Microsoft.EntityFrameworkCore;
using BookQuoteAPI.Data;
using BookQuoteAPI.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("BookQuoteDB"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngular");

app.MapGet("/api/books", async (AppDbContext context) =>
{
    return await context.Books.ToListAsync();
});

app.MapPost("/api/books", async (AppDbContext context, Book book) =>
{
    context.Books.Add(book);
    await context.SaveChangesAsync();
    return Results.Created($"/api/books/{book.Id}", book);
});

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
});

app.MapDelete("/api/books/{id}", async (AppDbContext context, int id) =>
{
    var book = await context.Books.FindAsync(id);
    if (book == null) return Results.NotFound();

    context.Books.Remove(book);
    await context.SaveChangesAsync();
    return Results.NoContent();
});
app.Run();

