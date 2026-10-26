using System.Collections.Generic;

namespace Brooklyneir.Gameplay
{
    public enum DrillKind { Returns, Precision, Control, Spin, Smash }

    public class DrillDefinition
    {
        public string Id;
        public DrillKind Kind;
        public string Title;
        public string Instructions;
        public int Balls;
        public int OneStar;
        public int TwoStars;
        public int ThreeStars;
        /// <summary>Height of the machine's feed: high balls invite smashes.</summary>
        public bool HighFeed;
    }

    public static class TrainingDrills
    {
        static List<DrillDefinition> _all;

        public static IList<DrillDefinition> All
        {
            get
            {
                if (_all == null)
                {
                    _all = new List<DrillDefinition>();
                    _all.Add(new DrillDefinition
                    {
                        Id = "returns", Kind = DrillKind.Returns, Title = "Práctica de golpes",
                        Instructions = "Devuelve las bolas de la máquina al campo contrario. Desliza hacia delante al golpear para ganar velocidad.",
                        Balls = 12, OneStar = 6, TwoStars = 9, ThreeStars = 11
                    });
                    _all.Add(new DrillDefinition
                    {
                        Id = "precision", Kind = DrillKind.Precision, Title = "Objetivos de precisión",
                        Instructions = "Haz botar la pelota dentro del aro dorado. Golpea en diagonal hacia arriba o hacia abajo para llevarla a ese lado; recto va al centro.",
                        Balls = 12, OneStar = 3, TwoStars = 6, ThreeStars = 9
                    });
                    _all.Add(new DrillDefinition
                    {
                        Id = "control", Kind = DrillKind.Control, Title = "Control izquierda-derecha",
                        Instructions = "Alterna: cada bola debe caer en la mitad que se ilumina. Golpea en diagonal hacia arriba para la mitad de arriba y hacia abajo para la de abajo.",
                        Balls = 12, OneStar = 5, TwoStars = 8, ThreeStars = 10
                    });
                    _all.Add(new DrillDefinition
                    {
                        Id = "spin", Kind = DrillKind.Spin, Title = "Práctica de efectos",
                        Instructions = "Golpea moviendo la pala rápido hacia arriba o hacia abajo, de lado y sin empujar hacia la red. Cuenta si la bola entra con efecto.",
                        Balls = 10, OneStar = 3, TwoStars = 6, ThreeStars = 8
                    });
                    _all.Add(new DrillDefinition
                    {
                        Id = "smash", Kind = DrillKind.Smash, Title = "Prueba de remates",
                        Instructions = "La máquina lanza bolas altas. Desliza rápido hacia la red justo al golpear para rematar.",
                        Balls = 10, OneStar = 3, TwoStars = 6, ThreeStars = 8, HighFeed = true
                    });
                }
                return _all;
            }
        }

        public static DrillDefinition Get(string id)
        {
            IList<DrillDefinition> all = All;
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return all[i];
            return all[0];
        }

        public static int StarsFor(DrillDefinition d, int successes)
        {
            if (successes >= d.ThreeStars) return 3;
            if (successes >= d.TwoStars) return 2;
            if (successes >= d.OneStar) return 1;
            return 0;
        }

        public static string Grade(int stars)
        {
            switch (stars)
            {
                case 3: return "¡PERFECTO!";
                case 2: return "MUY BIEN";
                case 1: return "APROBADO";
                default: return "SIGUE PRACTICANDO";
            }
        }
    }
}
