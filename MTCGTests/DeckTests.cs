using NUnit.Framework;
using Npgsql;
using SWE.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[TestFixture]
public class DeckTests
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
        using (var cmd = new NpgsqlCommand("DELETE FROM user_deck; DELETE FROM cards; DELETE FROM users;", _connection))
        {
            cmd.ExecuteNonQuery();
        }
    }

    [Test]
    public async Task GetDeckForUser_ShouldReturnEmptyDeckForUserWithNoDeck()
    {
        var user = new User { id = 2, username = "nouser" }; // Ensure a user with no deck exists

        await InsertUserAsync(user);

        // Act
        var result = await new Deck().GetDeckForUser(user);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Cards.Count); // Expect no cards in the deck
    }


    private async Task InsertUserAsync(User user)
    {
        using (var cmd = new NpgsqlCommand("INSERT INTO users (id, username) VALUES (@id, @username)", _connection))
        {
            cmd.Parameters.AddWithValue("@id", user.id);
            cmd.Parameters.AddWithValue("@username", user.username);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task InsertCardAsync(Card card)
    {
        using (var cmd = new NpgsqlCommand("INSERT INTO cards (id, name, damage, \"Element\", \"Type\") VALUES (@id, @name, @damage, @element, @type)", _connection))
        {
            cmd.Parameters.AddWithValue("@id", card.id);
            cmd.Parameters.AddWithValue("@name", card.name);
            cmd.Parameters.AddWithValue("@damage", card.damage);
            cmd.Parameters.AddWithValue("@element", card.Element);
            cmd.Parameters.AddWithValue("@type", card.Type);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

