using Microsoft.AspNetCore.Mvc;

namespace CreArte.ModelsPartial
{
    //public class CalendarViewModels : Controller
    //{
    //    public IActionResult Index()
    //    {
    //        return View();
    //    }
    //}

    public class CalendarEventDto
    {
        public string id { get; set; } = string.Empty;

        public string title { get; set; } = string.Empty;

        public DateTime start { get; set; }

        public DateTime? end { get; set; }

        public string color { get; set; } = "#666";

        public string url { get; set; } = string.Empty;

        public string type { get; set; } = string.Empty;
    }
}
