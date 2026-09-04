using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Api;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var events = context.RewardPointEvents.ToList();
    Console.WriteLine($"Total events: {events.Count}");
    foreach (var e in events) {
        Console.WriteLine($"Event: {e.Id} - Status: {e.Status} - Points: {e.Points}");
    }
}
