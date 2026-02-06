using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using Microsoft.EntityFrameworkCore;
using Npgsql.Internal;
using PaymentService.Contracts;
using PaymentService.Data;
// using PaymentService.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB

var connstr = builder.Configuration.GetConnectionString("PaymentDb");
builder.Services.AddDbContext<PaymentDbContext>(opt => opt.UseNpgsql(connstr));

// builder.Services.AddSingleton<InMemoryPaymentStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.MapGet("/" , ()  => "Hello from payment service");

app.MapGet("/health", () => Results.Ok(new {status = "ok"}));

// app.MapPost("/api/payments", (CreatePaymentRequest req, InMemoryPaymentStore  store) =>
// {
//     if (req.OrderId <= 0) return Results.BadRequest(new {error = "OrderId must be > 0"});
//     if (req.Amount <= 0) return Results.BadRequest(new {error = "Amount must be > 0"});

//     var payment = store.Create(req.OrderId, req.Amount);

//     var response = new PaymentResponse(
//         payment.Id,
//         payment.OrderId,
//         payment.Amount,
//         payment.status,
//         payment.CreatedAtUtc
//     );

//     return Results.Created($"/api/payments/{payment.Id}", response);
// }).WithName("CreatePayment");

// create payment

app.MapPost("/api/payments", async (CreatePaymentRequest req, PaymentDbContext db)=>
{
     if (req.OrderId <= 0) return Results.BadRequest(new {error = "OrderId must be > 0"});
     if (req.Amount <= 0) return Results.BadRequest(new {error = "Amount must be > 0"});

     var payment = new Payment
     {
         OrderId = req.OrderId,
         Amount = req.Amount,
         status = "SUCCESS",
         CreatedAtUtc = DateTime.UtcNow
     };

     db.Payments.Add(payment);
     await db.SaveChangesAsync();

     var response = new PaymentResponse(payment.Id, payment.OrderId, payment.Amount, payment.status, payment.CreatedAtUtc);
     return Results.Created($"/api/payments/{payment.Id}", response);

});

// app.MapGet("/api/payments/{id:int}", (int id, InMemoryPaymentStore store) =>
// {
//     var payment = store.Get(id);
//     if (payment is null) return Results.NotFound(new {error = "Payment not found"});

//     var response = new PaymentResponse(
//         payment.Id,
//         payment.OrderId,
//         payment.Amount,
//         payment.status,
//         payment.CreatedAtUtc
//     );

//     return Results.Ok(response);
// }).WithName("GetPaymentsById");

// GET PAYMENT BY ID

app.MapGet("/api/payments/{id:int}", async (int id, PaymentDbContext db ) =>
{
    var payment = await db.Payments.FindAsync(id);
    if (payment is null) return Results.NotFound(new {error = "Payment not found"});

    var response = new PaymentResponse(payment.Id, payment.OrderId, payment.Amount, payment.status, payment.CreatedAtUtc);

    return Results.Ok(response);
});

app.Run();


