<%@ Application Language="C#" %>
<script runat="server">

    void Application_Start(object sender, EventArgs e)
    {
        // OMDb is served over TLS 1.2. .NET 4.0 does not enable it by default, so
        // switch it on numerically - the SecurityProtocolType members for TLS 1.1
        // and 1.2 only exist from .NET 4.5 onwards.
        try
        {
            System.Net.ServicePointManager.SecurityProtocol =
                (System.Net.SecurityProtocolType)(768 | 3072) |
                System.Net.SecurityProtocolType.Tls;
        }
        catch
        {
            // Older framework on the host: OmdbClient falls back to plain HTTP.
        }

        System.Net.ServicePointManager.Expect100Continue = false;
    }

    void Application_Error(object sender, EventArgs e)
    {
        Exception error = Server.GetLastError();
        if (error is HttpUnhandledException && error.InnerException != null)
            error = error.InnerException;

        // ASP.NET rejects anything that looks like markup in a posted field. That
        // is worth keeping switched on, but a family member typing "I <3 this one"
        // deserves a sentence rather than a yellow error page.
        if (error is System.Web.HttpRequestValidationException)
        {
            Server.ClearError();
            try
            {
                if (Session != null)
                {
                    Session["Popcorn.Flash"] =
                        "Sorry - the < and > characters are not allowed in what you type. " +
                        "Please take them out and try again.";
                    Session["Popcorn.FlashKind"] = "error";
                }
            }
            catch
            {
                // Session state is not always available this early in the pipeline.
            }

            Response.Redirect("~/Default.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }

</script>
