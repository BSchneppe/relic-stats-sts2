using RelicStats.Core;

namespace RelicStats.Tests;

public class CardAcquisitionLedgerTests
{
    [Fact]
    public void Failed_acquisition_does_not_consume_an_offered_cards_provenance()
    {
        var ledger = new CardAcquisitionLedger<object, object>();
        var card = new object();
        var egg = new object();
        ledger.Record(card, new[] { egg });

        Assert.Empty(ledger.Acquire(card, success: false));
        Assert.Equal(new[] { egg }, ledger.Acquire(card, success: true));
        Assert.Empty(ledger.Acquire(card, success: true));
    }

    [Fact]
    public void A_clone_and_an_unpicked_offer_do_not_count_as_acquired()
    {
        var ledger = new CardAcquisitionLedger<object, object>();
        var picked = new object();
        var unpicked = new object();
        var clone = new object();
        var lens = new object();
        ledger.Record(picked, new[] { lens, lens });
        ledger.Record(unpicked, new[] { lens });

        Assert.Empty(ledger.Acquire(clone, success: true));
        Assert.Single(ledger.Acquire(picked, success: true));
        Assert.Empty(ledger.Acquire(picked, success: true));
        Assert.Single(ledger.Acquire(unpicked, success: true));
    }
}
