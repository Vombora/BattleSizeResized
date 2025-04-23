using TaleWorlds.Library;

namespace BattleSizeResized
{
    public static class Messenger
    {
        public static void Notify(string message, Context context = Context.None, string soundEventPath = "")
        {
            Color color = context switch
            {
                Context.Success => Colors.Green,
                Context.Error => Colors.Red,
                _ => Colors.White
            };

            if (soundEventPath != "")
            {
                InformationManager.DisplayMessage(new InformationMessage(message, color));
                InformationManager.DisplayMessage(new InformationMessage("", soundEventPath));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(message, color));
            }
        }
    }

    public enum Context
    {
        Success,
        Error,
        None
    }
}
