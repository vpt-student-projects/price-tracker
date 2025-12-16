using PriceTracker.Services;

namespace PriceTracker.Middleware
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public JwtMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Invoke(HttpContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            Console.WriteLine($"🔍 JwtMiddleware: Auth Header: {authHeader}");

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                Console.WriteLine($"🔍 JwtMiddleware: Token получен, длина: {token.Length}");

                if (token.Length > 10)
                {
                    Console.WriteLine($"🔍 JwtMiddleware: Первые 50 символов токена: {token.Substring(0, Math.Min(50, token.Length))}...");
                }

                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

                    Console.WriteLine($"🔍 JwtMiddleware: Вызываем GetUserFromToken...");
                    var user = authService.GetUserFromToken(token);

                    if (user != null)
                    {
                        context.Items["User"] = user;
                        Console.WriteLine($"✅ JwtMiddleware: Пользователь установлен в контекст: {user.Username}");

                        context.Response.Headers.Add("X-Authenticated-User", user.Username);
                        context.Response.Headers.Add("X-Authenticated-Role", user.Role);
                    }
                    else
                    {
                        Console.WriteLine($"❌ JwtMiddleware: GetUserFromToken вернул null");
                        context.Response.Headers.Add("X-Auth-Error", "Invalid token");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"💥 JwtMiddleware: Ошибка при проверке токена: {ex.Message}");
                    context.Response.Headers.Add("X-Auth-Error", ex.Message);
                }
            }
            else
            {
                Console.WriteLine($"ℹ️ JwtMiddleware: Нет заголовка Authorization или не начинается с Bearer");
            }

            await _next(context);
        }
    }
}