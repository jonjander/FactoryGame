namespace FactoryGame.Domain.Content;

/// <summary>MVP catalog: 20 elements; DNA values are illustrative v1 seeds.</summary>
public static class ElementCatalog
{
    public static IReadOnlyList<ElementDefinition> All { get; } = Build();

    private static ElementDefinition[] Build()
    {
        var dnas = new long[]
        {
            0x00010203_04050607L,
            0x01020304_05060708L,
            0x02030405_06070809L,
            0x03040506_0708090AL,
            0x04050607_08090A0BL,
            0x05060708_090A0B0CL,
            0x06070809_0A0B0C0DL,
            0x0708090A_0B0C0D0EL,
            0x08090A0B_0C0D0E0FL,
            0x090A0B0C_0D0E0F10L,
            0x0A0B0C0D_0E0F1011L,
            0x0B0C0D0E_0F101112L,
            0x0C0D0E0F_10111213L,
            0x0D0E0F10_11121314L,
            0x0E0F1011_12131415L,
            0x0F101112_13141516L,
            0x10111213_14151617L,
            0x11121314_15161718L,
            0x12131415_16171819L,
            0x13141516_1718191AL
        };

        var list = new ElementDefinition[20];
        for (var i = 0; i < 20; i++)
            list[i] = new ElementDefinition(i + 1, dnas[i], $"E{i + 1:D2}");
        return list;
    }
}
