using Hangfire.Dashboard;

namespace IISState.Filter;

public class DashboardNoAuthorizationFilter :IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // 允许所有用户访问
        return true;
    }
}