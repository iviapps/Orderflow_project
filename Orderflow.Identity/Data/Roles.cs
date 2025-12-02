namespace Orderflow.Identity.Data;

public static class Roles
{
    public const string Admin = nameof(Admin);
    public const string Customer = nameof(Customer);

    public static IEnumerable<string> GetAll()
    {
        yield return Admin;
        yield return Customer;
    }
}
