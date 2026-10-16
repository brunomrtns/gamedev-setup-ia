using System;
using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>
    /// Categorias de gêneros suportadas pela arquitetura modular do Projeto Setup A.
    /// </summary>
    public enum GameGenre
    {
        RunnerPlatformer,
        VehicleRacing,
        ActionShooter,
        Fighting,
        AdventureExploration,
        RpgSurvival
    }

    /// <summary>
    /// Contrato base para motores de movimentação (personagens bípedes, veículos, naves, etc.),
    /// permitindo que GameManager, testes de integração e controladores operem sem acoplamento
    /// exclusivo às regras de 3 faixas do primeiro jogo.
    /// </summary>
    public interface ICharacterMotor
    {
        bool CanMove { get; }
        void SetMovementActive(bool active);
        void ResetPosition();
        void StepSimulation(float deltaTime);
    }

    /// <summary>
    /// Contrato genérico de entrada para suportar ações arbitrárias por gênero
    /// (ex: "MoveLeft", "MoveRight", "Jump", "Slide", "Fire", "AttackLight", "Interact", "Steer").
    /// </summary>
    public interface IGameInputAdapter
    {
        event Action<string> OnActionTriggered;
        void TriggerAction(string actionId);
    }

    /// <summary>
    /// Descritor declarativo das capacidades exigidas por um jogo gerado pelo estúdio.
    /// </summary>
    [Serializable]
    public class GameCapabilityProfile
    {
        public string GameId = "neon-rush";
        public GameGenre Genre = GameGenre.RunnerPlatformer;
        public string RigType = "humanoid";
        public string[] RequiredAnimations = { "Idle", "RunCycle", "Jump", "Slide" };
        public string[] BoundActions = { "MoveLeft", "MoveRight", "Jump", "Slide", "Restart" };
    }
}
