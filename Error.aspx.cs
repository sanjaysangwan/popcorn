using System;

public partial class ErrorPage : PageBase
{
    protected override bool AllowAnonymous { get { return true; } }
    protected override bool SkipInstallCheck { get { return true; } }
}
