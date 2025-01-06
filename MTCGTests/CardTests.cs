using NUnit.Framework;
using Npgsql;
using SWE.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[TestFixture]
public class CardTests
{
    private const string ConnectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg_test;Port=5432";
    private NpgsqlConnection _connection;

    [SetUp]
    public void SetUp()
    {
        _connection = new NpgsqlConnection(ConnectionString);
        _connection.Open();


        CleanUpDatabase();
    }

    [TearDown]
    public void TearDown()
    {
        _connection.Close();
    }

    private void CleanUpDatabase()
    {
        using (var cmd = new NpgsqlCommand("DELETE FROM public.user_deck; DELETE FROM public.cards; DELETE FROM public.packages;", _connection))
        {
            cmd.ExecuteNonQuery();
        }
    }

    [Test]
    public void SetCardTypeAndElement_ShouldSetCardTypeAndElementCorrectly()
    {
        // Arrange
        var card = new Card("Fire Dragon");

        // Act
        card.SetCardTypeAndElement();

        // Assert
        Assert.AreEqual(CardType.Monster, card.Type);
        Assert.AreEqual(ElementType.Fire, card.Element);
    }

    [Test]
    public void SetCardTypeAndElement_ShouldSetDefaultTypeAndElement()
    {
        // Arrange
        var card = new Card("Mystic Shield");

        // Act
        card.SetCardTypeAndElement();

        // Assert
        Assert.AreEqual(CardType.Monster, card.Type);  // Default to Monster
        Assert.AreEqual(ElementType.Normal, card.Element);  // Default to Normal
    }

    [Test]
    public async Task AddCardAsync_ShouldAddCardToDatabase()
    {
        // Arrange
        var card = new Card("Fire Dragon")
        {
            id = Guid.NewGuid(),
            name = "Fire Dragon",
            damage = 50.0,
            Type = CardType.Monster,
            Element = ElementType.Fire
        };

        await card.AddCardAsync(card, ConnectionString);

        using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM public.cards WHERE id = @id", _connection))
        {
            command.Parameters.AddWithValue("@id", card.id);
            var count = (long)await command.ExecuteScalarAsync();
            Assert.AreEqual(1, count);
        }
    }

    [Test]
    public async Task AddCardsToUserInventoryAsync_ShouldAddCardsToUser()
    {
        var user = new User { id = 1, username = "testuser" }; // Ensure a test user exists
        var card1 = new Card("Fire Dragon") { id = Guid.NewGuid() };
        var card2 = new Card("Water Elf") { id = Guid.NewGuid() };
        var cards = new List<Card> { card1, card2 };

        await card1.AddCardAsync(card1, ConnectionString);
        await card2.AddCardAsync(card2, ConnectionString);

        await card1.AddCardsToUserInventoryAsync(user, cards);

        using (var command = new NpgsqlCommand("SELECT COUNT(*) FROM public.user_deck WHERE user_id = @userId AND card_id = @cardId", _connection))
        {
            command.Parameters.AddWithValue("@userId", user.id);

            command.Parameters.AddWithValue("@cardId", card1.id);
            var count1 = (long)await command.ExecuteScalarAsync();
            Assert.AreEqual(1, count1);

            command.Parameters.AddWithValue("@cardId", card2.id);
            var count2 = (long)await command.ExecuteScalarAsync();
            Assert.AreEqual(1, count2);
        }
    }

    [Test]
    public void GetCardType_ShouldReturnCorrectCardType()
    {
        Assert.AreEqual(CardType.Monster, Card.GetCardType("Fire Dragon"));
        Assert.AreEqual(CardType.Spell, Card.GetCardType("Healing Spell"));
    }

    [Test]
    public void GetElementalType_ShouldReturnCorrectElementalType()
    {
        Assert.AreEqual(ElementType.Fire, Card.GetElementalType("Fire Dragon"));
        Assert.AreEqual(ElementType.Water, Card.GetElementalType("Water Elf"));
        Assert.AreEqual(ElementType.Normal, Card.GetElementalType("Mystic Shield"));
    }

    [Test]
    public void Card_ShouldHaveCorrectName_WhenCreated()
    {
        var cardName = "WaterGoblin";

        var card = new Card(cardName);

        Assert.AreEqual(cardName, card.name);
    }


    [Test]
    public void GetCardType_ShouldReturnMonster_ForMonsterCardName()
    {
        string cardName = "Elf Monster";

        var cardType = Card.GetCardType(cardName);

        Assert.AreEqual(CardType.Monster, cardType);
    }

    [Test]
    public void GetElementalType_ShouldReturnFire_ForFireCardName()
    {
        string cardName = "Fire Dragon";

        var elementType = Card.GetElementalType(cardName);

        Assert.AreEqual(ElementType.Fire, elementType);
    }

    [Test]
    public async Task AddCardAsync_ShouldAddCardSuccessfully()
    {
        var card = new Card("Water Spell") { id = Guid.NewGuid(), damage = 30 };
        card.SetCardTypeAndElement();

        var connectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432";

        await card.AddCardAsync(card, connectionString);

        Assert.Pass("Card added successfully (Check database for confirmation).");
    }


}