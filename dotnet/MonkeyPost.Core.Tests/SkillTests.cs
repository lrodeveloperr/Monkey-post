using System;
using System.Collections.Generic;
using System.Linq;
using MonkeyPost.Core.Bots;
using Xunit;

namespace MonkeyPost.Core.Tests
{
    /// <summary>Skill moves, through balls, shielding and team strength.</summary>
    public class SkillTests
    {
        const float Dt = 1f / 60f;
        const int Carrier = 3;
        const int Defender = 6;

        static MatchSimulation InPlay(MatchConfig config = null)
        {
            var sim = new MatchSimulation(config ?? new MatchConfig(), seed: 1);
            sim.SkipKickoff();
            for (int id = 0; id < MatchSimulation.PlayerCount; id++)
                sim.PlacePlayer(id, new Vec2(4f + id * 1.5f, 0.6f), new Vec2(1f, 0f));
            return sim;
        }

        static PlayerInput[] Inputs(Action<PlayerInput[]> set = null)
        {
            var inputs = new PlayerInput[MatchSimulation.PlayerCount];
            set?.Invoke(inputs);
            return inputs;
        }

        static List<MatchEvent> Run(MatchSimulation sim, int ticks, Action<PlayerInput[]> set = null)
        {
            var events = new List<MatchEvent>();
            for (int i = 0; i < ticks; i++)
            {
                sim.Step(Inputs(set), Dt);
                events.AddRange(sim.Events);
            }
            return events;
        }

        /// <summary>Carrier dribbling along +X at jogging pace with the ball at his feet; a defender ahead.</summary>
        static MatchSimulation Duel(Vec2 defenderVelocity, float defenderX = 18.3f)
        {
            var sim = InPlay();
            sim.PlacePlayer(Carrier, new Vec2(16f, 10f), new Vec2(1f, 0f));
            sim.SetVelocity(Carrier, new Vec2(3f, 0f));
            sim.PlaceBall(new Vec2(16.5f, 10f), new Vec2(3f, 0f), owner: Carrier);
            sim.PlacePlayer(Defender, new Vec2(defenderX, 10f), new Vec2(-1f, 0f));
            sim.SetVelocity(Defender, defenderVelocity);
            return sim;
        }

        static void CarrierRunsAndDefenderCharges(PlayerInput[] x)
        {
            x[Carrier].Move = new Vec2(1f, 0f);
            x[Carrier].Sprint = true;
            x[Defender].Move = new Vec2(-1f, 0f);
        }

        [Fact]
        public void Nutmeg_GoesThroughADefenderMidStride()
        {
            var sim = Duel(new Vec2(-4f, 0f));
            var events = Run(sim, 1, x => { CarrierRunsAndDefenderCharges(x); x[Carrier].Skill = new Vec2(1f, 0f); });
            // Ball through the legs; the attacker goes round the defender to collect it.
            for (int i = 0; i < 150; i++)
            {
                var me = sim.Players[Carrier].Position;
                bool besideDefender = Vec2.Distance(me, sim.Players[Defender].Position) < 1.5f;
                var goTo = sim.Ball.Position + (besideDefender ? new Vec2(0f, 1.5f) : Vec2.Zero);
                events.AddRange(Run(sim, 1, x =>
                {
                    x[Carrier].Move = (goTo - me).Normalized;
                    x[Carrier].Sprint = true;
                    x[Defender].Move = new Vec2(-1f, 0f);
                }));
            }

            Assert.Contains(events, e => e.Type == MatchEventType.SkillAttempt && e.Detail == (int)SkillMove.Nutmeg);
            Assert.Contains(events, e => e.Type == MatchEventType.SkillBeat && e.Detail == (int)SkillMove.Nutmeg && e.PlayerB == Defender);
            Assert.Equal(0, MatchSimulation.TeamOf(sim.Ball.Owner));
        }

        [Fact]
        public void Nutmeg_IntoAPlantedDefender_HitsTheirLegs()
        {
            var sim = Duel(Vec2.Zero, defenderX: 18f);
            var events = Run(sim, 1, x => x[Carrier].Skill = new Vec2(1f, 0f));
            events.AddRange(Run(sim, 120, x => x[Carrier].Move = new Vec2(1f, 0f)));

            Assert.DoesNotContain(events, e => e.Type == MatchEventType.SkillBeat);
        }

        [Fact]
        public void DragBack_TurnsFasterThanPlantingAndTurning()
        {
            float VelocityAfterTurn(bool dragBack)
            {
                var sim = InPlay();
                sim.PlacePlayer(Carrier, new Vec2(16f, 10f), new Vec2(1f, 0f));
                sim.SetVelocity(Carrier, new Vec2(5f, 0f));
                sim.PlaceBall(new Vec2(16.5f, 10f), new Vec2(5f, 0f), owner: Carrier);
                Run(sim, 1, x =>
                {
                    x[Carrier].Move = new Vec2(-1f, 0f);
                    if (dragBack) x[Carrier].Skill = new Vec2(-1f, 0f);
                });
                Run(sim, 35, x => x[Carrier].Move = new Vec2(-1f, 0f));
                Assert.Equal(Carrier, sim.Ball.Owner);
                return sim.Players[Carrier].Velocity.X;
            }

            float withSkill = VelocityAfterTurn(true);
            float without = VelocityAfterTurn(false);
            Assert.True(withSkill < -1f, $"drag-back should have him going back already, vx {withSkill}");
            Assert.True(without > withSkill + 1f, $"plain turn {without} vs drag-back {withSkill}");
        }

        [Fact]
        public void StepOver_WrongFootsAWatchingDefender()
        {
            var sim = Duel(Vec2.Zero, defenderX: 18f);
            Run(sim, 1, x => x[Carrier].Skill = new Vec2(0f, 1f));

            var defender = sim.Players[Defender];
            Assert.True(defender.Velocity.Y > 1f, "defender should lean towards the fake");
            Assert.True(defender.TackleCooldown > 0.3f, "defender can't tackle while recovering");
            // The carrier then goes the other way.
            Assert.True(sim.Players[Carrier].FeintDirection.Y < 0f);
        }

        [Fact]
        public void SkillMoves_HaveACooldown()
        {
            var sim = Duel(Vec2.Zero);
            var events = Run(sim, 1, x => x[Carrier].Skill = new Vec2(0f, 1f));
            events.AddRange(Run(sim, 1, x => x[Carrier].Skill = new Vec2(0f, -1f)));
            Assert.Single(events, e => e.Type == MatchEventType.SkillAttempt);
        }

        [Fact]
        public void ThroughBall_IsPlayedIntoSpaceAheadOfTheRunner()
        {
            var sim = InPlay();
            const int passer = 1, runner = 4;
            sim.PlacePlayer(passer, new Vec2(12f, 10f), new Vec2(1f, 0f));
            sim.PlaceBall(new Vec2(12.5f, 10f), Vec2.Zero, owner: passer);
            sim.PlacePlayer(runner, new Vec2(18f, 13f), new Vec2(1f, 0f));
            sim.SetVelocity(runner, new Vec2(5f, 0f));

            var events = Run(sim, 1, x =>
            {
                x[passer].Pass = true;
                x[passer].ThroughBall = true;
                x[passer].Move = new Vec2(1f, 0.5f);
                x[runner].Move = new Vec2(1f, 0f);
            });
            var pass = events.Single(e => e.Type == MatchEventType.Pass);
            Assert.Equal(1, pass.Detail);
            Assert.Equal(runner, sim.Ball.IntendedReceiver);
            Assert.True(sim.Ball.IntendedTarget.X > sim.Players[runner].Position.X + 2.5f, "should lead the runner into space");

            // The runner keeps running into the space and collects it in stride.
            Run(sim, 180, x =>
            {
                var me = sim.Players[runner].Position;
                var goTo = Vec2.Distance(sim.Ball.Position, me) < 3f ? sim.Ball.Position : sim.Ball.IntendedTarget;
                x[runner].Move = (goTo - me).Normalized;
            });
            Assert.Equal(runner, sim.Ball.Owner);
        }

        [Fact]
        public void Shielding_KicksInForASlowCarrierAndProtectsTheBall()
        {
            float SuccessRate(bool fromBehind)
            {
                int wins = 0;
                const int trials = 200;
                for (int t = 0; t < trials; t++)
                {
                    var config = new MatchConfig { TackleSuccessChance = 1f };
                    var sim = new MatchSimulation(config, seed: (ulong)(t + 1));
                    sim.SkipKickoff();
                    for (int id = 0; id < MatchSimulation.PlayerCount; id++)
                        sim.PlacePlayer(id, new Vec2(4f + id * 1.5f, 0.6f), new Vec2(1f, 0f));
                    sim.PlacePlayer(Carrier, new Vec2(16f, 10f), new Vec2(1f, 0f));
                    sim.PlaceBall(new Vec2(16.45f, 10f), Vec2.Zero, owner: Carrier);
                    // From behind: the carrier's body is between tackler and ball.
                    var at = fromBehind ? new Vec2(15.1f, 10f) : new Vec2(17.3f, 10f);
                    sim.PlacePlayer(Defender, at, new Vec2(fromBehind ? 1f : -1f, 0f));
                    Run(sim, 1);
                    Assert.True(sim.Players[Carrier].IsShielding);
                    var events = Run(sim, 1, x => x[Defender].Tackle = true);
                    if (events.Any(e => e.Type == MatchEventType.Tackle)) wins++;
                }
                return wins / (float)trials;
            }

            float behind = SuccessRate(true);
            float front = SuccessRate(false);
            Assert.True(behind < front * 0.7f, $"from behind {behind:0.00} vs from the front {front:0.00}");
        }

        [Fact]
        public void Sprinting_CancelsShielding()
        {
            var sim = InPlay();
            sim.PlacePlayer(Carrier, new Vec2(16f, 10f), new Vec2(1f, 0f));
            sim.PlaceBall(new Vec2(16.45f, 10f), Vec2.Zero, owner: Carrier);
            sim.PlacePlayer(Defender, new Vec2(15.2f, 10f), new Vec2(1f, 0f));
            Run(sim, 1);
            Assert.True(sim.Players[Carrier].IsShielding);
            Run(sim, 30, x => { x[Carrier].Move = new Vec2(1f, 0f); x[Carrier].Sprint = true; });
            Assert.False(sim.Players[Carrier].IsShielding);
        }

        [Fact]
        public void StrongerTeams_AreAFewPercentQuicker()
        {
            var hard = BotProfile.TraitsFor(BotDifficulty.Hard);
            var easy = BotProfile.TraitsFor(BotDifficulty.Easy);
            Assert.InRange(hard.Pace, 1.01f, 1.06f);
            Assert.InRange(easy.Pace, 0.94f, 0.99f);
            Assert.True(BotProfile.TraitsFor(BotDifficulty.Medium, cupRound: 2).Pace > BotProfile.TraitsFor(BotDifficulty.Medium).Pace);

            var config = new MatchConfig();
            config.AwayTraits = hard;
            var sim = InPlay(config);
            sim.PlacePlayer(2, new Vec2(4f, 6f), new Vec2(1f, 0f));
            sim.PlacePlayer(7, new Vec2(4f, 14f), new Vec2(1f, 0f));
            Run(sim, 150, x =>
            {
                x[2].Move = x[7].Move = new Vec2(1f, 0f);
                x[2].Sprint = x[7].Sprint = true;
            });
            Assert.True(sim.Players[7].Velocity.X > sim.Players[2].Velocity.X + 0.1f);
        }

        [Fact]
        public void PersonsTeam_NeverGetsTraits()
        {
            var match = new LocalMatch(new MatchConfig(), home: null, away: BotDifficulty.Hard, seed: 1, cupRound: 2);
            var traits = match.Simulation.Config.HomeTraits;
            Assert.Equal(1f, traits.Pace);
            Assert.Equal(1f, traits.Tackling);
            Assert.Equal(1f, traits.Touch);
            Assert.True(match.Simulation.Config.AwayTraits.Pace > 1f);
        }

        [Fact]
        public void BotMatches_UseRunsThroughBallsAndSkills()
        {
            int throughBalls = 0, skills = 0, beats = 0;
            for (ulong seed = 1; seed <= 10; seed++)
            {
                var match = new LocalMatch(new MatchConfig(), BotDifficulty.Hard, BotDifficulty.Hard, seed);
                var sim = match.Simulation;
                while (sim.Phase != Phase.Ended)
                {
                    match.Tick(Dt);
                    foreach (var e in sim.Events)
                    {
                        if (e.Type == MatchEventType.Pass && e.Detail == 1) throughBalls++;
                        if (e.Type == MatchEventType.SkillAttempt) skills++;
                        if (e.Type == MatchEventType.SkillBeat) beats++;
                    }
                }
            }
            Assert.True(throughBalls >= 10, $"through balls: {throughBalls}");
            Assert.True(skills >= 10, $"skill attempts: {skills}");
            Assert.True(beats >= 2, $"skills that beat a defender: {beats}");
        }
    }
}
