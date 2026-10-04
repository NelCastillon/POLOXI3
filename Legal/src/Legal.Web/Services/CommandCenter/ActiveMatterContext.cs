namespace Legal.Web.Services.CommandCenter;

// Circuit-scoped memory of the matter the user is currently working with. The top-nav
// "Case Command Center" link is static (no matterId), so without this the link always lands on
// the empty "Open a Matter" state even right after the user opened a matter. Pages that resolve a
// concrete matter record it here; the Command Center restores it when opened without a matterId.
public sealed class ActiveMatterContext
{
    public Guid? MatterId { get; private set; }

    public void Set(Guid matterId)
    {
        if (matterId != Guid.Empty)
            MatterId = matterId;
    }
}
