function LoadDummy()
{
	window.OpenRA = {};
	OpenRA.version = "{DUMMY_VERSION}";
	OpenRA.GetSequence = function () {
		return "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg==";
	};
}