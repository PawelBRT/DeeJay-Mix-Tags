using System.Drawing;
using System.Windows.Forms;

namespace Mp3TaggerGUI
{
    internal static class AppUiStyle
    {
        public static readonly Color AppBackColor = Color.FromArgb(245, 247, 250);
        public static readonly Color TextColor = Color.FromArgb(45, 45, 48);
        public static readonly Color BorderColor = Color.FromArgb(200, 205, 215);
        public static readonly Color PrimaryColor = Color.FromArgb(0, 120, 212);

        public static void ApplyForm(Form form)
        {
            form.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            form.BackColor = AppBackColor;
        }

        public static void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = BorderColor;
            button.BackColor = Color.White;
            button.ForeColor = TextColor;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseCompatibleTextRendering = true;
            button.Cursor = Cursors.Hand;
        }

        public static void StylePrimaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = PrimaryColor;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
        }
    }
}
