using System;
using Dissonance.Datastructures;

namespace Dissonance.Networking.Client
{
    internal interface IClient
    {
        ushort? LocalId { get; }

        uint SessionId { get; }

        [NotNull] string PlayerName { get; }

        [NotNull] ConcurrentPool<byte[]> ByteBufferPool { get; }

        void OnPlayerJoined(string playerName);

        void OnPlayerLeft(string playerName);

        void OnVoicePacketReceived(VoicePacket obj);

        void OnTextPacketReceived(TextMessage obj);

        void OnPlayerStartedSpeaking(string playerName);

        void OnPlayerStoppedSpeaking(string playerName);
        
        void SendReliable(ArraySegment<byte> packet);

        void SendUnreliable(ArraySegment<byte> packet);
    }

    internal interface IPacketProcessor
    {
        void ReceivePlayerRoutingUpdate(ref PacketReader reader);

        void ReceiveVoiceData(ref PacketReader reader);

        void ReceiveTextData(ref PacketReader reader);

        void ReceiveHandshakeResponse(ref PacketReader reader);
    }
}
