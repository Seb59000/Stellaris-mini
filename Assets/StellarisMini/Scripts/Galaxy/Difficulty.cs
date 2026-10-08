using UnityEngine;

namespace StellarisMini
{
    /// Réglages d'un niveau de difficulté. Ils agissent à la fois sur la carte galactique
    /// (économie et agressivité des rivaux) et sur les combats (dégâts infligés et subis).
    public class DifficultyLevel
    {
        public string name;
        public string description;

        // Carte galactique
        public float aiProduction;      // multiplicateur de production des empires IA
        public float playerProduction;  // multiplicateur de production du joueur
        public int graceDays;           // jours de paix au début de la partie
        public float attackRatio;       // supériorité exigée par l'IA avant d'attaquer
        public float minAttackPower;    // puissance minimale d'une flotte IA pour attaquer
        public float siegeMul;          // durée des sièges contre les systèmes du joueur

        // Combats contre le joueur (pilotés ou résolus automatiquement)
        public float enemyDamage;       // dégâts infligés par l'ennemi
        public float playerDamage;      // dégâts infligés par le camp du joueur
        public float playerFocus;       // tendance des ennemis à viser le vaisseau du joueur

        // Bonus de départ du joueur
        public int bonusAlloys, bonusFighters, bonusCorvettes;
    }

    public static class Difficulty
    {
        public const int Default = 2;

        public static readonly DifficultyLevel[] Levels =
        {
            new DifficultyLevel
            {
                name = "Très facile",
                description = "Pour découvrir le jeu : rivaux lents et peu agressifs, combats très indulgents.",
                aiProduction = 0.5f, playerProduction = 1.3f, graceDays = 1440, attackRatio = 1.8f, minAttackPower = 100f, siegeMul = 1.8f,
                enemyDamage = 0.45f, playerDamage = 1.35f, playerFocus = 0f,
                bonusAlloys = 150, bonusFighters = 1, bonusCorvettes = 1,
            },
            new DifficultyLevel
            {
                name = "Facile",
                description = "Rivaux moins productifs, longue période de paix, ennemis moins dangereux en combat.",
                aiProduction = 0.72f, playerProduction = 1.15f, graceDays = 1020, attackRatio = 1.5f, minAttackPower = 75f, siegeMul = 1.4f,
                enemyDamage = 0.7f, playerDamage = 1.15f, playerFocus = 2f,
                bonusAlloys = 75, bonusFighters = 1, bonusCorvettes = 0,
            },
            new DifficultyLevel
            {
                name = "Normale",
                description = "L'expérience prévue : rivaux à armes égales.",
                aiProduction = 1f, playerProduction = 1f, graceDays = 660, attackRatio = 1.25f, minAttackPower = 55f, siegeMul = 1f,
                enemyDamage = 1f, playerDamage = 1f, playerFocus = 6f,
            },
            new DifficultyLevel
            {
                name = "Difficile",
                description = "Rivaux plus productifs et plus pressés d'attaquer.",
                aiProduction = 1.3f, playerProduction = 1f, graceDays = 480, attackRatio = 1.1f, minAttackPower = 50f, siegeMul = 0.9f,
                enemyDamage = 1.15f, playerDamage = 1f, playerFocus = 8f,
            },
            new DifficultyLevel
            {
                name = "Très difficile",
                description = "Pour les amiraux aguerris : l'ennemi frappe fort et attaque très tôt.",
                aiProduction = 1.6f, playerProduction = 1f, graceDays = 330, attackRatio = 1f, minAttackPower = 45f, siegeMul = 0.8f,
                enemyDamage = 1.3f, playerDamage = 1f, playerFocus = 10f,
            },
        };

        public static DifficultyLevel Get(int index)
        {
            return Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];
        }
    }
}
