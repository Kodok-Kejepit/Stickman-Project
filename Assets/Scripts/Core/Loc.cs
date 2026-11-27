namespace Stickman.Core
{
    /// <summary>Zero-allocation localization strings for Indonesian ("id") and English ("en").</summary>
    public static class Loc
    {
        public static string Get(string key, string lang)
        {
            bool en = lang == "en";
            switch (key)
            {
                case "mode.free": return en ? "Free" : "Bebas";
                case "mode.drill": return en ? "Straight Drill" : "Drill Lurus";
                case "mode.course": return en ? "Chain Course" : "Rute Rantai";
                case "tools.btn": return en ? "Tools" : "Alat";
                case "tools.inf_energy": return en ? "Inf Energy" : "Energi Penuh";
                case "tools.no_cd": return en ? "No CD" : "Tanpa CD";
                case "tools.slow": return en ? "Slow 0.5x" : "Lambat 0.5x";
                case "tools.dummy": return en ? "+ Dummy" : "+ Dummy";
                case "tools.patrol": return en ? "+ Patrol" : "+ Patroli";
                case "tools.clear": return en ? "Clear Bots" : "Hapus Bot";
                case "tools.tp_mid": return "TP Mid";
                case "tools.tp_blue": return "TP Blue";
                case "tools.tp_red": return "TP Red";
                case "tools.tp_exp": return "TP EXP";
                case "tools.tp_gold": return "TP Gold";
                case "tools.tp_base": return "TP Base";
                case "skill.basic": return en ? "ATK" : "SER";
                case "skill.spin": return en ? "S1 SPIN" : "S1 GESEK";
                case "skill.cable": return en ? "CABLE" : "KABEL";
                case "skill.ult": return "ULT";
                case "skill.cancel": return en ? "CANCEL" : "BATAL";
                case "settings.title": return en ? "SETTINGS" : "PENGATURAN";
                case "settings.fps": return en ? "Target FPS" : "Target FPS";
                case "settings.pitch": return en ? "Camera Pitch" : "Sudut Kamera";
                case "settings.zoom": return en ? "Camera Distance" : "Jarak Kamera";
                case "settings.sens": return en ? "Aim Sensitivity" : "Sensitivitas Aim";
                case "settings.deadzone": return en ? "Joystick Deadzone" : "Deadzone Joystick";
                case "settings.aimguide": return en ? "Show Aim Guide" : "Garis Bantu Aim";
                case "settings.autospin": return en ? "Auto Spin (Fly-Through)" : "Gesek Otomatis";
                case "settings.shake": return en ? "Landing Shake" : "Getar Mendarat";
                case "settings.showfps": return en ? "Show FPS" : "Tampilkan FPS";
                case "settings.lang": return en ? "Language" : "Bahasa";
                case "settings.layout": return en ? "Edit Button Layout" : "Atur Layout Tombol";
                case "settings.reset": return en ? "Reset Defaults" : "Reset Awal";
                case "settings.close": return en ? "Close" : "Tutup";
                case "layout.title": return en ? "DRAG BUTTONS TO REPOSITION" : "GESER TOMBOL UNTUK MENGATUR";
                case "layout.save": return en ? "Save" : "Simpan";
                case "layout.cancel": return en ? "Cancel" : "Batal";
                case "layout.reset": return en ? "Reset" : "Reset";
                case "energy.label": return en ? "ENERGY" : "ENERGI";
                default: return key;
            }
        }
    }
}
