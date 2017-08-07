namespace Dissonance.Networking
{
    internal struct ChannelBitField
    {
        private const byte TypeMask = 0x01;         //00000001
        private const byte PositionalMask = 0x02;   //00000010
        private const byte ClosureMask = 0x04;      //00000100

        private const byte PriorityOffset = 3;
        private const byte PriorityMask = 0x18;     //00011000

        private const byte SessionIdOffset = 5;
        private const byte SessionIdMask = 0x61;    //01100000

        private readonly byte _bitfield;
        public byte Bitfield
        {
            get { return _bitfield; }
        }

        public ChannelType Type
        {
            get
            {
                if ((_bitfield & TypeMask) == TypeMask)
                    return ChannelType.Room;
                return ChannelType.Player;
            }
        }

        public bool IsClosing
        {
            get { return (_bitfield & ClosureMask) == ClosureMask; }
        }

        public bool IsPositional
        {
            get { return (_bitfield & PositionalMask) == PositionalMask; }
        }

        public ChannelPriority Priority
        {
            get
            {
                var val = (_bitfield & PriorityMask) >> PriorityOffset;
                switch (val)
                {
                    default: return ChannelPriority.Default;
                    case 1: return ChannelPriority.Low;
                    case 2: return ChannelPriority.Medium;
                    case 3: return ChannelPriority.High;
                }
            }
        }

        public int SessionId
        {
            get { return (_bitfield & SessionIdMask) >> SessionIdOffset; }
        }

        public ChannelBitField(byte bitfield)
        {
            _bitfield = bitfield;
        }

        public ChannelBitField(ChannelType type, int sessionId, ChannelPriority priority, bool positional, bool closing)
            : this()
        {
            _bitfield = 0;

            //Pack the single bit values by setting their flags
            if (type == ChannelType.Room)
                _bitfield |= TypeMask;
            if (positional)
                _bitfield |= PositionalMask;
            if (closing)
                _bitfield |= ClosureMask;

            //Pack priority by shiftnig bits into position
            switch (priority)
            {
                case ChannelPriority.Low:
                    _bitfield |= 1 << PriorityOffset;
                    break;
                case ChannelPriority.Medium:
                    _bitfield |= 2 << PriorityOffset;
                    break;
                case ChannelPriority.High:
                    _bitfield |= 3 << PriorityOffset;
                    break;

                // ReSharper disable RedundantCaseLabel, RedundantEmptyDefaultSwitchBranch (justification: I like to be explicit about these things)
                case ChannelPriority.None:
                case ChannelPriority.Default:
                default:
                    break;
                // ReSharper restore RedundantCaseLabel, RedundantEmptyDefaultSwitchBranch
            }

            //Pack session ID by wrapping it as a 2 bit number and then shifting bits into position
            _bitfield |= (byte)((sessionId % 4) << SessionIdOffset);
        }
    }
}
