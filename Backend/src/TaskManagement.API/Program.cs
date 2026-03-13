// Nhớ thêm thư viện này để dùng DbContext và SQL Server
using Microsoft.EntityFrameworkCore; 

var builder = WebApplication.CreateBuilder(args);

// 1. Mở tính năng Controllers (Chuẩn bị cho các API Login, Task...)
builder.Services.AddControllers();

builder.Services.AddOpenApi();

// 2. Khai báo Policy CORS
var myAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: myAllowSpecificOrigins,
                      policy =>
                      {
                          // Cho phép Vue.js gọi vào
                          policy.WithOrigins("http://localhost:5173") 
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

// 3. CẤU HÌNH CODE-FIRST (ENTITY FRAMEWORK CORE)
// Lưu ý cho PM: Tôi đang comment đoạn này lại để không bị báo lỗi đỏ. 
// Chiều nay Dev 1 tạo xong file 'ApplicationDbContext' bên thư mục Infrastructure thì mới mở comment ra!
/*
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
*/

var app = builder.Build();

// ---------------- CẤU HÌNH PIPELINE ----------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 4. KÍCH HOẠT CORS (Vị trí cực kỳ quan trọng, phải đứng trước Authorization)
app.UseCors(myAllowSpecificOrigins);

app.UseAuthorization();

// 5. Nối các endpoint vào Controllers
app.MapControllers();

app.Run();