namespace BlazorIdle.Services
{
    public class ApiAuthException : Exception
    {
        public ApiAuthException(string message) : base(message) { }
    }
}
