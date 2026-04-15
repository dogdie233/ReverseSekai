using MessagePack;

namespace SelfHostSekai.Realtime;

/// <summary>
/// Binary protocol compatible with Diarkis packet format.
/// Layout: [ver:4][cmd:4][status:4][payloadSize:4][payload:N]
/// All integers little-endian.
/// </summary>
public static class RealtimeProtocol
{
    public const int HeaderSize = 16;

    // ── Diarkis status codes ──
    public const uint StatusOk = 1;
    public const uint StatusBad = 4;
    public const uint StatusErr = 5;

    // ── Diarkis Room commands ──
    public const uint RoomCreate       = 100;
    public const uint RoomJoin         = 101;
    public const uint RoomLeave        = 102;
    public const uint RoomBroadcast    = 103;
    public const uint RoomMessage      = 104;
    public const uint RoomUpdateProp   = 107;
    public const uint RoomGetProp      = 108;
    public const uint RoomGetOwner     = 109;
    public const uint RoomGetMembers   = 11;
    public const uint RoomOwnerChange  = 14;
    public const uint RoomPropSync     = 130;

    // ── Diarkis MatchMaker commands ──
    public const uint MatchSearch      = 201;
    public const uint MatchLeave       = 203;
    public const uint MatchSync        = 204;
    public const uint MatchComplete    = 206;

    // ── CP.Realtime custom commands ──
    public const uint CustomRandJoin           = 1010;
    public const uint CustomRandRoomJoin       = 10111;
    public const uint PlayerInfo               = 1011;
    public const uint RoomSync                 = 1012;
    public const uint RoomSyncMinimal          = 1022;
    public const uint CloseMatchmake           = 1013;
    public const uint CloseRoom                = 1024;
    public const uint ChangeMatchmakeCondition = 1014;
    public const uint AddMatchmakeCondition    = 1034;
    public const uint OpenRoom                 = 1023;
    public const uint ScaleUpMatchmake         = 1015;
    public const uint ResetScaleUp             = 1025;
    public const uint MatchingMoveAndScaleUp   = 1035;
    public const uint CustomCreate             = 1016;
    public const uint UpdateRoomProperty       = 10010;
    public const uint UpdatePlayerProperty     = 10020;
    public const uint UpdatePlayerPropAndIdx   = 10030;
    public const uint CustomJoin               = 1019;
    public const uint UnlockJoin               = 1049;
    public const uint JoinPostProcess          = 1032;
    public const uint JoinPostProcessMinimal   = 1042;
    public const uint RefreshUserIndex         = 1021;
    public const uint ReleasePrivateRoom       = 2000;
    public const uint MatchingRoomMove         = 1020;
    public const uint DirectRoomMove           = 1029;
    public const uint ForceDirectRoomMove      = 1059;
    public const uint CustomRecreate           = 1300;
    public const uint TimestampCmd             = 10000;
    public const uint ChangeOwner              = 998;
    public const uint RoomMember               = 600;

    // ── MultiLive message commands (broadcast via RoomMessage) ──
    // Note: these IDs are message payload IDs, not Diarkis cmd IDs.
    // They are sent wrapped in RoomBroadcast (103) or RoomMessage (104).
    // We define them here for dispatch in HandleBroadcastAsync.
    public const uint MsgCountDown             = 1000;
    public const uint MsgStamp                 = 1001;
    public const uint MsgLoadProgress          = 1002;
    // MsgPlayerLiveInfo = 2000 — same value as ReleasePrivateRoom, handled via RoomBroadcast
    public const uint MsgPlayerPraiseInfo      = 2001;
    public const uint MsgPlayerSkillInfo       = 2002;

    // ── MultiLive custom matchmaking commands ──
    public const uint MultiLiveRandJoin              = 3000;
    public const uint MultiLiveRandRoomJoin          = 3001;
    public const uint MultiLiveCloseMatchmake        = 3030;
    public const uint MultiLiveAddMatchmakeCondition = 3040;
    public const uint MultiLiveScaleUpMatchmake      = 3050;
    public const uint MultiLiveCustomCreate          = 3060;
    public const uint MultiLiveCreate                = 3070;
    public const uint MultiLiveCustomJoin            = 3080;
    public const uint ReleaseMultiLivePrivateRoom    = 3090;
    public const uint MultiLivePrivateCloseMatchmake = 3100;
    public const uint MultiLiveReStartPrivate        = 3110;
    public const uint MultiLiveUnlockJoin            = 3120;
    public const uint UpdateTotalPowerLimit          = 10040;

    public static Packet Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
            throw new InvalidDataException($"Packet too short: {data.Length} < {HeaderSize}");

        var ver = BitConverter.ToUInt32(data[..4]);
        var cmd = BitConverter.ToUInt32(data[4..8]);
        var status = BitConverter.ToUInt32(data[8..12]);
        var payloadSize = BitConverter.ToInt32(data[12..16]);

        byte[]? payload = null;
        if (payloadSize > 0)
        {
            if (data.Length < HeaderSize + payloadSize)
                throw new InvalidDataException($"Payload truncated: need {payloadSize}, have {data.Length - HeaderSize}");
            payload = data.Slice(HeaderSize, payloadSize).ToArray();
        }

        return new Packet(ver, cmd, status, payload);
    }

    public static byte[] Encode(uint ver, uint cmd, uint status, byte[]? payload = null)
    {
        var payloadLen = payload?.Length ?? 0;
        var buf = new byte[HeaderSize + payloadLen];

        BitConverter.TryWriteBytes(buf.AsSpan(0, 4), ver);
        BitConverter.TryWriteBytes(buf.AsSpan(4, 4), cmd);
        BitConverter.TryWriteBytes(buf.AsSpan(8, 4), status);
        BitConverter.TryWriteBytes(buf.AsSpan(12, 4), payloadLen);

        if (payload != null)
            payload.CopyTo(buf, HeaderSize);

        return buf;
    }

    public static byte[] EncodeResponse(uint cmd, uint status, object? body = null)
    {
        byte[]? payload = null;
        if (body != null)
            payload = MessagePackSerializer.Serialize(body.GetType(), body);

        return Encode(2, cmd, status, payload);
    }

    public static byte[] EncodeOk(uint cmd, object? body = null)
        => EncodeResponse(cmd, StatusOk, body);

    public static byte[] EncodeError(uint cmd)
        => EncodeResponse(cmd, StatusErr);

    public record Packet(uint Ver, uint Cmd, uint Status, byte[]? Payload);
}
