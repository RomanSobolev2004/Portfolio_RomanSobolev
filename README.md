<div align="center">
  
  # 👾 Welcome to my Code Forge!
  
  <a href="https://github.com/DenverCoder1/readme-typing-svg">
    <img src="https://readme-typing-svg.herokuapp.com?font=Fira+Code&weight=600&size=24&pause=1000&color=2E8B57&center=true&vCenter=true&width=600&lines=Hi,+I'm+Roman+Sobolev!;Junior+Unity+%26+C%23+Developer;Passionate+about+SOLID+%26+Clean+Code;Games+Programming+Student" alt="Typing SVG" />
  </a>

  <p>
    <em>Transforming coffee into performant C# code and building architectures that don't collapse on the first bug. ☕🚀</em><br>
  </p>

  <p>
    <img src="https://img.shields.io/badge/Unity-100000?style=for-the-badge&logo=unity&logoColor=white" alt="Unity" />
    <img src="https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white" alt="C#" />
    <img src="https://img.shields.io/badge/Architecture-SOLID%20%7C%20MVC-blue?style=for-the-badge" alt="Architecture" />
  </p>
</div>

<br>

<div align="center">


# 🎮 Paper Tanks Mobile
**A mobile 2D turn‑based strategy game about tanks in a hand‑drawn style.**

[![Unity Version](https://img.shields.io/badge/Unity-2021.3LTS-black?style=flat-square&logo=unity)](https://unity.com/)
[![Status](https://img.shields.io/badge/Paused-orange.svg?style=flat-square)]()

<img src="./project_examples/paper_tanks_mobile/gifs/paper_tanks_mobile_showcase.gif" width="700" alt="Gameplay Showcase">

*Short gameplay showcase*

</div>

---

## 📖 About The Project

Paper Tanks Mobile is a game being developed as a personal project, which will later be refined as part of the semester program at SAE Institute. The main goal of the project is to adapt a school board game, wrapped in a more dynamic gameplay experience and featuring unique hand‑drawn artwork by Garfinkel.

At the moment, the project has reached the prototype stage and currently paused.

## 🛠 Tech Stack & Tools

* **Engine:** Unity 2021.3.2f1 LTS
* **Language:** C#
* **Architecture:** MVC, Component-Based Design
* **Dependency Injection:** simple scene/project-context injecting, using [Zenject by modesttree](https://github.com/modesttree/zenject)
* **Other Plugins/Tools:** DOTween, NaughtyAttributes

## 🏗 Architecture & Engineering Highlights

* **Turn-based system:** A turn-based gameplay based on State Machine has been created, along with basic algorithms for analyzing and searching for targets for enemies, with the addition of error margins.
* **Event-Driven Communication:** Replaced tight coupling with an Event Bus / ScriptableObject-based event system to handle UI and gameplay communication.
* **Sound system:** A simple but functional system for sound and music, controlled from the game menu.
* **Units & Grid:** The game field is represented as a mathematical grid that supports various types of units and objects, as well as a unit and object spawning system based on the Prototype pattern.

## Code examples links

* [Turn-base system](./project_examples/paper_tanks_mobile/code/turn_system.cs)
* [Enemies algorithms](./project_examples/paper_tanks_mobile/code/enemies.cs)
* [Grid](./project_examples/paper_tanks_mobile/code/grid.cs)
* [Units](./project_examples/paper_tanks_mobile/code/units.cs)
