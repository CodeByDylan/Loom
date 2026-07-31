namespace Loom.Paging.Tests;

public sealed class PagingTests
{
    private static readonly int[] Numbers = [.. Enumerable.Range(1, 25)];

    [Test]
    public async Task A_Request_Computes_Its_Offset()
    {
        await Assert.That(new PageRequest(1, 10).Skip).IsEqualTo(0);
        await Assert.That(new PageRequest(3, 10).Skip).IsEqualTo(20);
    }

    [Test]
    public async Task A_Page_Number_Below_One_Is_Rejected()
    {
        await Assert.That(() => new PageRequest(0, 10)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new PageRequest(-1, 10)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task A_Page_Size_Below_One_Is_Rejected() => await Assert.That(() => new PageRequest(1, 0)).Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task A_Page_Size_Over_The_Maximum_Is_Rejected()
    {
        // An unbounded page size is a denial-of-service vector, so the limit lives in the type rather
        // than in a validator someone might forget to write.
        await Assert.That(() => new PageRequest(1, PageRequest.DefaultMaximumSize + 1))
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => new PageRequest(1, 51, maximumSize: 50))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task First_Is_Page_One_At_The_Largest_Size_Allowed()
    {
        // Pinned because the size is the one thing about First a reader could reasonably expect to be
        // smaller. Nothing in this repo calls it — it exists for consumers — so without this the
        // property could change size and no build would notice.
        await Assert.That(PageRequest.First.Number).IsEqualTo(1);
        await Assert.That(PageRequest.First.Size).IsEqualTo(PageRequest.DefaultMaximumSize);
        await Assert.That(PageRequest.First.Skip).IsEqualTo(0);
    }

    [Test]
    public async Task A_Custom_Maximum_Is_Honoured()
    {
        PageRequest request = new(1, 500, maximumSize: 1000);

        await Assert.That(request.Size).IsEqualTo(500);
        await Assert.That(request.MaximumSize).IsEqualTo(1000);
    }

    [Test]
    public async Task ApplyPaging_Takes_One_Page()
    {
        int[] page = [.. Numbers.ApplyPaging(new PageRequest(3, 10))];

        await Assert.That(page.Length).IsEqualTo(5);
        await Assert.That(page[0]).IsEqualTo(21);
    }

    [Test]
    public async Task ApplyPaging_Works_On_A_Query()
    {
        int[] page = [.. Numbers.AsQueryable().ApplyPaging(new PageRequest(2, 10))];

        await Assert.That(page[0]).IsEqualTo(11);
    }

    [Test]
    public async Task ToPage_Counts_And_Takes()
    {
        Page<int> page = Numbers.ToPage(new PageRequest(2, 10));

        await Assert.That(page.Items.Count).IsEqualTo(10);
        await Assert.That(page.TotalCount).IsEqualTo(25);
        await Assert.That(page.Number).IsEqualTo(2);
        await Assert.That(page.TotalPages).IsEqualTo(3);
    }

    [Test]
    public async Task A_Page_Knows_Its_Neighbours()
    {
        Page<int> first = Numbers.ToPage(new PageRequest(1, 10));
        Page<int> middle = Numbers.ToPage(new PageRequest(2, 10));
        Page<int> last = Numbers.ToPage(new PageRequest(3, 10));

        await Assert.That(first.HasPrevious).IsFalse();
        await Assert.That(first.HasNext).IsTrue();
        await Assert.That(middle.HasPrevious).IsTrue();
        await Assert.That(middle.HasNext).IsTrue();
        await Assert.That(last.HasNext).IsFalse();
    }

    [Test]
    public async Task A_Page_Past_The_End_Is_Empty_But_Still_Reports_The_Total()
    {
        Page<int> page = Numbers.ToPage(new PageRequest(9, 10));

        await Assert.That(page.Items.Count).IsEqualTo(0);
        await Assert.That(page.TotalCount).IsEqualTo(25);
        await Assert.That(page.HasNext).IsFalse();
    }

    [Test]
    public async Task An_Empty_Source_Has_No_Pages()
    {
        Page<int> page = Array.Empty<int>().ToPage(new PageRequest(1, 10));

        await Assert.That(page.TotalCount).IsEqualTo(0);
        await Assert.That(page.TotalPages).IsEqualTo(0);
        await Assert.That(page.HasNext).IsFalse();
        await Assert.That(page.HasPrevious).IsFalse();
    }

    [Test]
    public async Task Empty_Describes_A_Request_That_Matched_Nothing()
    {
        var page = Page<int>.Empty(new PageRequest(2, 10));

        await Assert.That(page.Items.Count).IsEqualTo(0);
        await Assert.That(page.Number).IsEqualTo(2);
        await Assert.That(page.TotalPages).IsEqualTo(0);
    }

    [Test]
    public async Task Null_Arguments_Are_Rejected()
    {
        await Assert.That(() => Numbers.ApplyPaging(null!).ToList()).Throws<ArgumentNullException>();
        await Assert.That(() => Numbers.ToPage(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => Page<int>.Empty(null!)).Throws<ArgumentNullException>();
    }
}
