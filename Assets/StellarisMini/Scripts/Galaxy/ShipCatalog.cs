namespace StellarisMini
{
    /// Caractéristiques d'une classe de vaisseau (stratégie + combat).
    public class ShipSpec
    {
        public ShipClass cls;

        // Stratégie
        public int alloyCost;
        public float buildDays;
        public float upkeep;          // énergie / mois
        public float galaxySpeed;     // unités / jour sur la carte
        public float power;           // puissance militaire indicative

        // Combat
        public float hull, shield;
        public float maxSpeed, accel, turnRate; // unités/s, unités/s², degrés/s
        public float radius;

        // Canon principal (tire dans l'axe du vaisseau)
        public float gunDamage, gunCooldown, gunSpeed, gunRange, gunSpread;
        public float gunWidth;

        // Missiles à tête chercheuse
        public float missileDamage, missileCooldown;
        public int missileSalvo;

        // Tourelles automatiques
        public int turrets;
        public float turretDamage, turretCooldown, turretRange;

        /// Dégâts par seconde approximatifs (sert à la résolution automatique).
        public float Dps
        {
            get
            {
                float d = gunDamage / gunCooldown;
                if (missileSalvo > 0) d += missileDamage * missileSalvo / missileCooldown * 0.8f;
                if (turrets > 0) d += turrets * turretDamage / turretCooldown * 0.7f;
                return d * 0.55f; // tous les tirs ne touchent pas
            }
        }
    }

    public static class ShipCatalog
    {
        static readonly ShipSpec[] specs =
        {
            new ShipSpec
            {
                cls = ShipClass.Chasseur, alloyCost = 30, buildDays = 20, upkeep = 0.5f, galaxySpeed = 1.6f, power = 10,
                hull = 60, shield = 30, maxSpeed = 17, accel = 34, turnRate = 300, radius = 0.9f,
                gunDamage = 5, gunCooldown = 0.16f, gunSpeed = 48, gunRange = 22, gunSpread = 2.5f, gunWidth = 0.25f,
            },
            new ShipSpec
            {
                cls = ShipClass.Corvette, alloyCost = 60, buildDays = 40, upkeep = 1f, galaxySpeed = 1.3f, power = 24,
                hull = 160, shield = 80, maxSpeed = 12, accel = 20, turnRate = 170, radius = 1.35f,
                gunDamage = 12, gunCooldown = 0.32f, gunSpeed = 42, gunRange = 26, gunSpread = 1.5f, gunWidth = 0.35f,
                missileDamage = 30, missileCooldown = 4f, missileSalvo = 1,
            },
            new ShipSpec
            {
                cls = ShipClass.Croiseur, alloyCost = 150, buildDays = 90, upkeep = 2f, galaxySpeed = 1.0f, power = 65,
                hull = 520, shield = 220, maxSpeed = 7.5f, accel = 10, turnRate = 75, radius = 2.4f,
                gunDamage = 45, gunCooldown = 1.1f, gunSpeed = 32, gunRange = 32, gunSpread = 0.8f, gunWidth = 0.7f,
                missileDamage = 30, missileCooldown = 6f, missileSalvo = 2,
                turrets = 2, turretDamage = 6, turretCooldown = 0.5f, turretRange = 20,
            },
        };

        public static ShipSpec Get(ShipClass c) { return specs[(int)c]; }
    }
}
