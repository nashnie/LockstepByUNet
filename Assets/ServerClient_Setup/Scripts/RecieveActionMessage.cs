namespace UnityEngine.Networking.NetworkSystem
{
    public class RecieveActionMessage : MessageBase
    {
        public byte[] value;
        public int LockStepTurnID;
        public string playerID;
    }
}
