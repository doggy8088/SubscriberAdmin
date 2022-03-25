namespace WebApplication1.Models
{
    public class Subscriber
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Photo { get; set; }
        public string Email { get; set; }

        public string LINEUserId { get; set; }
        public string LINELoginAccessToken { get; set; }
        public string LINELoginIDToken { get; set; }
        public string LINENotifyAccessToken { get; set; }
    }
}
