This game is architected entirely around the idea of using lifetimes.
Whether or not that's a good idea, or a good example... I dunno.
At least it's not as boring as the other example project.

Basic overview of the architecture:

- MainWindow is the 'root' where everything is basically triggered
	~ You can scan down the file, using 'go to definition' where you want more details

- "Drawing" is done in the most naive way possible: creating and moving ellipse/rectangle/line controls.
	~ Replacing the drawing code should be easy, because that code has very low coupling to the game code.
	~ The 'drawing' code is mostly in GameMechanics/GameDrawAsControlsUtilities.cs

- PerishableCollection is a fantastic concept, I think, and I (ab)use it here for some very good decoupling
	~ For example, the game loop is a perishable collection of actions: anything can insert itself into the game loop
	~ Many mechanics 'observe' the collection of living balls/connectors and insert loop actions for each one that have a the observed thing's lifetime
	~ That's also how drawing is so decoupled
