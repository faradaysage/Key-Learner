
# Key Learner

**Key Learner** is an educational app designed to make learning fun and interactive for toddlers and young children. Inspired by [Scott Hanselman's "Baby Smash"](https://www.babysmash.com/), it builds upon the original concept with improved graphics, modularity, and learning modes tailored to different developmental stages. The app includes features like vibrant particle effects, voice feedback for letters and words, and support for a customizable word dictionary.

---

## Features

### 1. **Toddler Smash Mode**
- Interactive mode for toddlers to press keys and see letters or words displayed on the screen with vibrant particle effects.
- Sounds out letters and words to aid early language development.
- Highlights matching words from a dictionary when spelled correctly.

### 2. **Improved Word Dictionary**
- Supports multiple CSV-based dictionaries, allowing for expanded vocabularies.
- Tracks minimum and maximum word lengths for enhanced gameplay.
- Easily customizable for parents to add new words or themes.

### 3. **Graphics and Effects**
- Built with **MonoGame** for dynamic, hardware-accelerated graphics.
- Integrated **BepuPhysics** for realistic particle effects and simulations.
- Additive and alpha-blended sprite rendering for visually engaging animations.

### 4. **Modularity**
- Designed with modularity in mind to allow easy addition of new learning modes (e.g., word association games, math challenges).
- Includes the first mode, "Toddler Smash," as a foundation.

### 5. **Keyboard Lock**
- Aims to restrict unintended key combinations, preventing toddlers from accidentally minimizing or closing the application.
- Future plans to introduce a parent-only exit mechanism using a complex key combination.

---

## Future Plans
- **Enhanced Keyboard Lock:** Implement a foolproof lock to ensure the app remains active during use.
- **New Modes and Games:** Add educational modes like math challenges, word association games, and visual puzzles.
- **Parental Controls:** Include settings for customizing dictionary files, themes, and particle effects.
- **Cross-Platform Support:** Expand beyond Windows to macOS and Linux.

---

## Installation

### Prerequisites
- [.NET 8.0](https://dotnet.microsoft.com/)
- **MonoGame Framework** (installed via NuGet)
- **BepuPhysics** and **BepuUtilities**:
  - Clone the BepuPhysics repository:
    ```bash
    git clone https://github.com/bepu/bepuphysics2.git
    ```
  - Build the project:
    ```bash
    cd bepuphysics2/BepuPhysics
    dotnet build -c Release
    ```
  - Ensure the compiled libraries are available at the expected paths:
    - `../bepuphysics2/BepuPhysics/bin/Release/net8.0/BepuPhysics.dll`
    - `../bepuphysics2/BepuPhysics/bin/Release/net8.0/BepuUtilities.dll`

---

### Running the App
1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/key-learner.git
   cd key-learner
   ```
2. Restore dependencies and build:
   ```bash
   dotnet restore
   dotnet build
   ```
3. Run the app:
   ```bash
   dotnet run
   ```

---

## Project Structure
```plaintext
KeyLearner/
├── Modes/                   # Modular game modes (e.g., Toddler Smash)
├── Resources/               # Fonts, textures, and graphical assets
├── data/                    # CSV dictionaries for customizable word lists
├── Content/                 # MonoGame content files
├── Core/                    # Core services and interfaces (e.g., voice, mode discovery)
├── Program.cs               # Entry point
├── Game1.cs                 # Main game logic
├── KeyLearner.csproj        # Project file
```

---

## Technologies Used

### Game Framework
- **MonoGame Framework**: Handles rendering, input, and game loop management.

### Graphics and Physics
- **BepuPhysics**: Simulates realistic particle effects and dynamics for a visually engaging experience.

### Text-to-Speech
- **System.Speech**: Provides voice feedback for letters and words to assist in learning.

---

## How to Contribute
Contributions are welcome! Here are some ways you can help:
- Suggest or implement new game modes.
- Improve the word dictionary with additional languages or themes.
- Optimize performance for low-end devices.

---

## Acknowledgments
- [Scott Hanselman](https://www.babysmash.com/) for inspiring this project with "Baby Smash."
- The developers of MonoGame and BepuPhysics for their excellent libraries.

---

## License
This project is licensed under the MIT License. See the `LICENSE` file for details.
