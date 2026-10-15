# Monkey Post — Physics Reference

The physics is calibrated against **real-world measurements**, not tuned by feel. Every number
below is enforced by an automated test in
[`RealWorldBenchmarkTests.cs`](../dotnet/MonkeyPost.Core.Tests/RealWorldBenchmarkTests.cs). If a
future tuning change breaks one of these tests, the change is wrong, not the test.

All physics is in `core/` (pure C#, deterministic). The match steps at 60 Hz and the ball at
240 Hz.

---

## 1. Benchmarks: real world vs game

### Ball and surfaces (FIFA Quality Programme test methods)

| Test | Real-world reference | Game |
|---|---|---|
| Ball calibration: 2.0 m drop on concrete, rebound | **1.35 ± 0.03 m** (FIFA sets match-ball pressure to this) | **1.35 m** (Port Harcourt concrete) |
| Turf vertical rebound, 2.0 m drop | **0.60–0.85 m** (FIFA Quality Pro turf) | **0.66 m** (Lagos cage) |
| Turf reduced ball roll, 1 m ramp | **4–8 m** (FIFA Quality Pro turf) | **6.9 m** (Lagos cage) |
| Natural grass rebound / roll | **0.60–1.00 m / 4–10 m** (FIFA Quality grade) | **0.62 m / 5.2 m** (Enugu hillside) |
| Asphalt court | livelier and faster than turf | 1.17 m / 12.6 m (Abuja) |
| Dusty earth | slower and deader than FIFA turf | 0.46 m / 3.3 m, bumpy (Ibadan) |
| Sand | barely bounces, dies quickly | 0.17 m / 1.6 m (Kano) |

The ramp roll assumes the ball leaves the 1 m ramp rolling (v = √(2gh / (1 + 2/3)) ≈ 3.4 m/s for a
hollow sphere).

### Aerodynamics

| Quantity | Real-world reference | Game |
|---|---|---|
| Drag coefficient, slow ball | ≈ 0.4–0.45 (subcritical) | 0.45 at 6 m/s |
| Drag crisis | centred near **12 m/s** for a football | 0.33 at 12 m/s (mid-crisis) |
| Drag coefficient, hard-hit ball | roughly halves past the crisis | 0.20 at 22 m/s |
| Spin and drag | drag rises with spin (Goff & Carré) | +0.3 × S, capped at +0.12 |
| Lift coefficient, typical curled free kick (S ≈ 0.22) | **≈ 0.25** (Goff & Carré trajectory analysis) | **0.24** |
| Lift at high spin (S = 1) | saturates (Goff & Carré measured up to S = 1) | 0.37 |
| Curled free kick: 25 m/s, 8 rev/s sidespin, over 25 m | about a second in the air, bends a few metres | 1.29 s, bends 4.4 m |
| Long kick: 30 m/s at 40° | far shorter than the 90 m vacuum range | 54.9 m |

S = rω/v is the spin parameter.

### Players (sports-science sprint and change-of-direction data)

| Quantity | Real-world reference | Game (street footballers) |
|---|---|---|
| Sprint model | mono-exponential v(t) = vmax(1 − e^(−t/τ)), standard in sprint profiling | same model |
| Initial acceleration A0 | **6.2–8.4 m/s²** (professionals) | 7.0 m/s² |
| Top speed | **≈ 9.2–9.5 m/s** (professionals' theoretical maximum) | 8.0 m/s (street level, below pros) |
| 10 m from standing | pros ≈ 1.7–1.9 s | 2.23 s |
| 30 m from standing | | 4.88 s |
| Maximum braking | high decelerations in football are typically counted above ≈ 4 m/s² | 6.5 m/s² (8 m/s to stop in ≈ 1.2 s, ≈ 5 m) |
| Curved sprint at near top speed | run on **≈ 7–11 m radii** | 8.0 m radius (grip 8 m/s², ≈ 0.8 g) |
| Maximal sprint duration | ≈ 6–7 s before slowing | stamina bar lasts ≈ 6.5 s |

---

## 2. The model

### Ball (hollow sphere, I = ⅔mr², r = 0.11 m, m = 0.43 kg: FIFA size 5)
- **Air:** gravity, drag ½ρC_d A v² with the drag crisis and spin-dependent drag, and Magnus
  lift ½ρC_l A v² along ω × v with C_l = S / (2.32 S + 0.4). Spin decays slowly in flight.
- **Ground:** a struck ball skids, and friction μ_s·g spins it up until it rolls (a hollow sphere
  keeps 3/5 of its speed). Rolling deceleration a + b·v: constant rolling resistance plus a
  speed-proportional term for grass and infill drag.
- **Contacts** (ground bounces, walls, posts, player bodies, the keeper's leg): impulses with
  restitution and Coulomb friction at the contact point, so spin and velocity trade off. Backspin
  checks up, topspin kicks on, and side-spin carries off a wall. Footballs grip during oblique
  bounces (measured friction-to-normal ratio ≈ 1.0, Cross), so bounce friction is high.
- **Restitution falls with impact speed** (−0.8% per m/s above 6.3 m/s, floor 60%): a hard shot
  rebounds proportionally less than a gentle drop.

### Players
- **Propulsion:** a = (vmax − v)/τ, τ = 1.15 s.
- **Friction circle:** speeding up, braking and turning share one 8 m/s² grip budget. Lateral
  acceleration v·ω uses what's left after the forward component, so turning radius grows with
  speed squared.
- **Plant and turn:** asked to turn more than ≈100° at speed, a player brakes first.
- **Bodies:** vertical cylinders the ball deflects off; players shoulder each other with
  momentum.

### Ball control
- Dribbling is a series of real touches (lead 0.6 m jogging, 2.2 m sprinting) with a touch assist.
- **First touch:** clean up to 6 m/s incoming. Reach for a clean touch shrinks for fast balls (half
  reach at 15 m/s), and above 15 m/s the ball deflects instead.
- **Kicks** are planned with the same physics: curl is compensated so shots arrive where aimed, and
  passes are weighted to arrive rolling at about 5 m/s.

---

## 3. Known limits (honest list)

No football game simulates everything. These are deliberate simplifications:
- **Knuckleballs:** the unsteady side forces on a near-spinless ball (seam and panel effects) are
  not modelled.
- **Wind, humidity, rain and a wet ball** are not modelled (all pitches are dry, still air).
- **Ball deformation during the kick** is replaced by planned launch velocity and spin plus
  accuracy errors. This is standard in football games.
- **Player bodies** are cylinders, not animated limbs. Contacts come from the physics, not from
  animation, until the character animation milestone.
- **Turf grain and directional effects** are not modelled.

---

## 4. Sources

- FIFA Quality Programme for Football Turf, Test Manual II: Test Requirements (vertical ball
  rebound 0.60–0.85 m Pro / 0.60–1.00 m Quality; reduced ball roll 4–8 m Pro / 4–10 m Quality):
  <https://digitalhub.fifa.com/m/44e19efa8822f5e/original/FIFA-quality-programme-for-football-turf-Test-Manual-II-Test-requirements-April-2024-edition.pdf>
- FIFA Quality Programme for Football Turf, Test Manual I: Test Methods (2.0 m drop; ball set to
  1.35 ± 0.03 m rebound on concrete):
  <https://digitalhub.fifa.com/m/f13b1cd18027f40/original/FIFA-quality-programme-for-football-turf-Test-Manual-I-Test-Methods-2015v-3-4.pdf>
- Goff & Carré, *Soccer ball lift coefficients via trajectory analysis*, Eur. J. Phys. 31 (2010):
  <https://faculty.lynchburg.edu/goff_j/Goff_Carre_EJP_2010.pdf>
- Goff, Carré et al., *Investigations into soccer aerodynamics via trajectory analysis*:
  <https://eprints.whiterose.ac.uk/id/eprint/98035/1/1-s2.0-S1877705812016414-main.pdf>
- *Power and spin in the beautiful game*, Physics Today (drag crisis near 12 m/s):
  <https://pubs.aip.org/physicstoday/article/63/7/62/391075/Power-and-spin-in-the-beautiful-game>
- J.W.M. Bush, *The aerodynamics of the beautiful game*:
  <https://thales.mit.edu/bush/wp-content/uploads/2013/11/Beautiful-Game-2013.pdf>
- Rod Cross, football bounce and spin measurements:
  <https://www.physics.sydney.edu.au/~cross/PUBLICATIONS/49.%20Football.pdf>,
  <https://www.physics.usyd.edu.au/~cross/PUBLICATIONS/31.%20Spin.pdf>
- *The Acceleration-Speed Profile in Professional Soccer Players* (S0 ≈ 9.18–9.47 m/s,
  A0 ≈ 6.20–8.41 m/s²): <https://jhk.termedia.pl/The-Acceleration-Speed-Profile-in-Professional-Soccer-Players-nA-Comparative-Study,220252,0,2.html>
- Haugen & Buchheit, sprint running performance monitoring review:
  <https://martin-buchheit.net/wp-content/uploads/2015/11/haugen-buchheit-sprint-monitoring-sports-med-review-in-press.pdf>
- Altmann et al., *Curved sprinting in soccer: The influence of radius* (radii 7.15–11.15 m):
  <https://journals.sagepub.com/doi/abs/10.1177/17479541231193807>
