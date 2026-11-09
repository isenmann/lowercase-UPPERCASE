namespace LowercaseUppercase
{
    /// <summary>What a letter touching an enemy leads to.</summary>
    public enum EnemyTouch
    {
        /// <summary>The letter burns.</summary>
        Hurts,
        /// <summary>The letter landed on it and bounces off. What happened to the enemy is up to the enemy.</summary>
        Stomped,
        /// <summary>Nothing happens to the letter.</summary>
        Harmless,
    }

    /// <summary>
    /// Lets a movement component decide what a touch does, for enemies that don't just die to a stomp
    /// and hurt otherwise, such as the curl that turns into a shell to kick.
    /// </summary>
    public interface IEnemyTouchRule
    {
        EnemyTouch Touch(LetterController letter, bool fromAbove);
    }
}
