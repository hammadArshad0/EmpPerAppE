using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.UI.WebControls;

namespace EmpPerAppE
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // ===============================
            // 1️⃣ Enable CORS for React frontend
            // ===============================
            // Allow only your React dev server
            var cors = new EnableCorsAttribute(
                origins: "http://localhost:5173", // React app
                headers: "*",
                methods: "*"
            );
            config.EnableCors(cors);

            // ===============================
            // 2️⃣ Enable Attribute Routing
            // ===============================
            config.MapHttpAttributeRoutes();

            // ===============================
            // 3️⃣ Force JSON Response (Fix XML Serialization)
            // ===============================
            config.Formatters.JsonFormatter.SupportedMediaTypes
                .Add(new MediaTypeHeaderValue("text/html"));

            // Remove XML formatter completely
            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // ===============================
            // 4️⃣ Default Route
            // ===============================
            config.Routes.MapHttpRoute(
       name: "DefaultApi",
       routeTemplate: "api/{controller}/{id}",
       defaults: new { id = System.Web.Http.RouteParameter.Optional }
   );

        }
    }
}
