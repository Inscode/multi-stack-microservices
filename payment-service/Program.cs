using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using Microsoft.EntityFrameworkCore;
using Npgsql.Internal;
using PaymentService.Contracts;
using PaymentService.Data;
using Serilog;
using FluentValidation;
using PaymentService.Validation;
using System.ComponentModel.DataAnnotations;
// using PaymentService.Storage;

var builder = WebApplication.CreateBuilder(args);

// Use serilog ( reads config from appsettings.json)

builder.Host.UseSerilog((ctx, services, cfg) => 
    cfg.ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// var pwd = Environment.GetEnvironmentVariable("DB_PASSWORD");

// if (string.IsNullOrEmpty(pwd))
//     throw new Exception("DB_PASSWORD is not set");

// DB

var connstr = builder.Configuration.GetConnectionString("PaymentDb");
// connstr = connstr.Replace("${DB_PASSWORD}", pwd);

builder.Services.AddDbContext<PaymentDbContext>(opt => opt.UseNpgsql(connstr));
builder.Services.AddScoped<IValidator<CreatePaymentRequest>, CreatePaymentRequestValidator>();
// builder.Services.AddSingleton<InMemoryPaymentStore>();

var app = builder.Build();

// Request logging (method/path/status/time)
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.MapGet("/" , ()  => "Hello from payment service");



app.MapGet("/health", () => 
{
    Log.Information("Health check called");
    Results.Ok(new {status = "ok"});
    
});

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

app.MapPost("/api/payments", async (CreatePaymentRequest req, IValidator<CreatePaymentRequest> validator, PaymentDbContext db)=>
{

     var result = await validator.ValidateAsync(req);

     if  (!result.IsValid)
    {
        return Results.BadRequest(new
        {
            error = "Validation Failed",
            details = result.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage})
        });
    }
    
     Log.Information("Create payment requested: OrderId={OrderId}, Amount={Amount}", req.OrderId, req.Amount);
    //  if (req.OrderId <= 0) return Results.BadRequest(new {error = "OrderId must be > 0"});
    //  if (req.Amount <= 0) return Results.BadRequest(new {error = "Amount must be > 0"});
     var payment = new Payment
     {
         OrderId = req.OrderId,
         Amount = req.Amount,
         status = "SUCCESS",
         CreatedAtUtc = DateTime.UtcNow
     };

     db.Payments.Add(payment);

     try
    {
       await db.SaveChangesAsync(); 
    }

    catch (DbUpdateException ex)
    when (ex.InnerException?.Message.Contains("duplicate") == true)
    {
        Log.Warning(ex, "DB update failed while creating payment for OrderId={OrderId}", req.OrderId);
        return Results.Conflict(new
        {
            error = "Payment already exists for this order"
        });
    }
     
     Log.Information("Payment created successfully: PaymentId={PaymentId}, OrderId={OrderId}", payment.Id, payment.OrderId);
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
    Log.Information("Get payment requested: PaymentId={PaymentId}", id);
    var payment = await db.Payments.FindAsync(id);
    if (payment is null) 
    {
        Log.Information("Payment not found: PaymentId = {PaymentId}", id);
        return Results.NotFound(new {error = "Payment not found"});
    }
    var response = new PaymentResponse(payment.Id, payment.OrderId, payment.Amount, payment.status, payment.CreatedAtUtc);

    return Results.Ok(response);
});

app.Run();


