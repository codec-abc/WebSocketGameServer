public static class WebSocketMessages
{
    public static readonly int CreateLobbyRequest = 0;

    public static readonly int JoinLobbyRequest = 1;
    public static readonly int LobbyCreated = 3;

    public static readonly int GameStart = 4;

    public static readonly int MoveCommand = 5;

    public static readonly int UpdateGameState = 6;

    public static readonly int PingRequest = 7;

    public static readonly int PingAnswer = 8;
}

public static class Utils
{
    public static byte[] MergeArrays(byte[] a1, byte[] a2)
    {
        byte[] rv = new byte[a1.Length + a2.Length];
        System.Buffer.BlockCopy(a1, 0, rv, 0, a1.Length);
        System.Buffer.BlockCopy(a2, 0, rv, a1.Length, a2.Length);

        return rv;
    }
}