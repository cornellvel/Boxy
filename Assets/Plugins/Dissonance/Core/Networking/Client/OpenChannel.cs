namespace Dissonance.Networking.Client
{
    internal struct OpenChannel
    {
        private static readonly Log Log = Logs.Create(LogCategory.Network, typeof(OpenChannel).Name);

        private readonly ChannelProperties _config;

        private readonly ChannelType _type;
        private readonly ushort _recipient;
        private readonly bool _isClosing;
        private readonly ushort _sessionId;

        [NotNull] public ChannelProperties Config
        {
            get { return _config; }
        }

        public byte Bitfield
        {
            get
            {
                return new ChannelBitField(
                    _type,
                    _sessionId,
                    Priority,
                    IsPositional,
                    _isClosing
                ).Bitfield;
            }
        }

        public ushort Recipient
        {
            get { return _recipient; }
        }


        public ChannelType Type
        {
            get { return _type; }
        }

        public bool IsClosing
        {
            get { return _isClosing; }
        }

        // ReSharper disable once MemberCanBePrivate.Global (Justificiation: Public API)
        public bool IsPositional
        {
            get { return _config.Positional; }
        }

        // ReSharper disable once MemberCanBePrivate.Global (Justificiation: Public API)
        public ChannelPriority Priority
        {
            get { return _config.TransmitPriority; }
        }

        public ushort SessionId
        {
            get { return _sessionId; }
        }

        public OpenChannel(ChannelType type, ushort sessionId, ChannelProperties config, bool closing, ushort recipient)
        {
            _type = type;
            _sessionId = sessionId;
            _config = config;
            _isClosing = closing;
            _recipient = recipient;
        }

        public OpenChannel AsClosing()
        {
            if (IsClosing)
                throw Log.CreatePossibleBugException("Attempted to close a channel which is already closed", "94ED6728-F8D7-4926-9058-E23A5870BF31");

            return new OpenChannel(_type, _sessionId, _config, true, _recipient);
        }

        public OpenChannel AsOpen()
        {
            if (!IsClosing)
                throw Log.CreatePossibleBugException("Attempted to open a channel which is already open", "F1880EDD-D222-4358-9C2C-4F1C72114B62");

            return new OpenChannel(_type, (ushort)(_sessionId + 1), _config, false, _recipient);
        }
    }
}
