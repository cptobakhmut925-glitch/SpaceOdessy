using SDL3;
using System.Diagnostics;
using System.Drawing;

const int kScreenHeight = 600;

var game = new Game
{
    Player = new GameObject
    {
        Position = new PointF
        {
            X = 50,
            Y = kScreenHeight - 10,
        },
        Speed = 300f,
        Color = Color.Purple,
        IsPlayerControlled = true,
        IsAsteroid = false,
    },
    ScreenSize = new Size
    {
        Width = 800,
        Height = kScreenHeight,
    },
};

game.Init();
game.Run();
game.Deinit();

class GameObject
{
    public PointF Position { get; set; }

    public SizeF Size { get; set; }

    public float Speed { get; set; }

    public Color Color { get; set; }

    public bool IsAsteroid { get; set; }

    public bool IsPlayerControlled { get; set; }

    public Texture? Texture { get; set; }
}

class Texture
{
    public nint SdlTexture { get; set; }
    
    public SizeF Size { get; set; }
}

class Game
{
    const byte kSDLAlphaOpaque = (byte)SDL.AlphaOpaque;

    public bool IsRunning { get; set; } = true;

    public GameObject? Player { get; set; }

    public Size ScreenSize { get; set; }

    public List<GameObject> gameObjects = [];

    int score = 0;

    readonly Color background = Color.DarkOrange;

    static readonly TimeSpan kSpawnRate = TimeSpan.FromSeconds(0.5);
    readonly Stopwatch spawnTimer = Stopwatch.StartNew();

    public int renderCount = 0;
    readonly Stopwatch fpsTimer = Stopwatch.StartNew();

    readonly Random random = new();

    Texture? templateCoinTexture = null;
    Texture? templateAsteroidTexture = null;
    Texture? templatePlayerTexture = null;

    nint window;
    nint renderer;
    nint textEngine;
    nint font;

    public void Init()
    {
        SDL.Init(SDL.InitFlags.Video);
        TTF.Init();

        SDL.CreateWindowAndRenderer(
            "SpaceOdessy",
            ScreenSize.Width, ScreenSize.Height,
            0, out window, out renderer);
    }

    public void Deinit()
    {
        SDL.DestroyWindow(window);
        SDL.DestroyRenderer(renderer);

        TTF.Quit();
        SDL.Quit();
    }

    public static Texture LoadTexture(nint renderer, string filename) 
    {
        var texture = Image.LoadTexture(renderer, filename);
        SDL.GetTextureSize(texture, out var w, out var h);
        return new Texture
        {
            SdlTexture = texture,
            Size = new SizeF
            {
                Width = w,
                Height = h
            }
        };
    }

    public void Run()
    {
        templateCoinTexture = LoadTexture(renderer, "assets/star_medium.png");
        templateAsteroidTexture = LoadTexture(renderer, "assets/meteor_1.png");
        templatePlayerTexture = LoadTexture(renderer, "assets/player_ship_C.png");

        if (Player is not null)
        {
            Player.Texture = templatePlayerTexture;
            Player.Size = templatePlayerTexture.Size;
            Player.Position = new PointF(Player.Position.X, Player.Position.Y - Player.Size.Height);
        }

        font = TTF.OpenFont("assets/OpenSans-Regular.ttf", 32);
        textEngine = TTF.CreateRendererTextEngine(renderer);

        var lastUpdate = TimeSpan.Zero;
        while (IsRunning)
        {
            var currentTime = TimeSpan.FromMilliseconds(SDL.GetTicksNS() / 1_000_000.0);

            while (SDL.PollEvent(out var @event))
            {
                switch (@event.Type)
                {
                    case (uint)SDL.EventType.Quit:
                        IsRunning = false;
                        break;
                    default:
                        // Ignore
                        break;
                }
            }

            var timeScale = 1.0f;
            var keyboardState = SDL.GetKeyboardState(out var _);
            if (keyboardState[(int)SDL.Scancode.Space])
            {
                timeScale = 2.0f;
            }

            var dtUpdate = currentTime - lastUpdate;
            if (dtUpdate > TimeSpan.FromSeconds(1.0 / 30.0))
            {
                Update(dtUpdate * timeScale);
                lastUpdate = currentTime;
            }

            Render();
        }

        TTF.CloseFont(font);

        SDL.DestroyTexture(templatePlayerTexture.SdlTexture);
        SDL.DestroyTexture(templateCoinTexture.SdlTexture);
        SDL.DestroyTexture(templateAsteroidTexture.SdlTexture);

        TTF.DestroyRendererTextEngine(textEngine);
    }

    void CountFPS()
    {
        renderCount++;
        if (fpsTimer.Elapsed > TimeSpan.FromSeconds(1))
        {
            Console.WriteLine($"FPS: {renderCount}");
            fpsTimer.Restart();
            renderCount = 0;
        }
    }

    public void Update(TimeSpan dt)
    {
        Console.WriteLine($"Sec {dt.TotalMinutes}");

        if (Player is null)
        {
            IsRunning = false;
            return;
        }

        var keyboardState = SDL.GetKeyboardState(out var _);
        var positionDelta = 0;
        if (keyboardState[(int)SDL.Scancode.Left])
        {
            positionDelta += -1;
        }
        if (keyboardState[(int)SDL.Scancode.Right])
        {
            positionDelta += 1;
        }

        var newPosition = Player.Position;
        newPosition.X += positionDelta * Player.Speed * (float)dt.TotalSeconds;
        Player.Position = newPosition;

        if (spawnTimer.Elapsed > kSpawnRate)
        {
            var isAsteroid = random.Next(2) == 0;
            if (isAsteroid)
            {
                if (templateAsteroidTexture is not null)
                {
                    gameObjects.Add(new GameObject
                    {
                        Size = templateAsteroidTexture.Size,
                        Color = Color.Gray,
                        Position = new PointF(
                            (float)random.NextDouble() * (ScreenSize.Width - templateAsteroidTexture.Size.Width), 0),
                        IsAsteroid = true,
                        Speed = 150f + (float)random.NextDouble() * 100f,
                        Texture = templateAsteroidTexture,
                    });
                }
            }
            else
            {
                if (templateCoinTexture is not null)
                {
                    gameObjects.Add(new GameObject
                    {
                        Size = templateCoinTexture.Size,
                        Color = Color.Gold,
                        Position = new PointF(
                            (float)random.NextDouble() * (ScreenSize.Width - templateCoinTexture.Size.Width), 0),
                        IsAsteroid = false,
                        Speed = 100f + (float)random.NextDouble() * 50f,
                        Texture = templateCoinTexture,
                    });
                }
            }

            spawnTimer.Restart();
        }

        for (int index = gameObjects.Count - 1; index >= 0; index--)
        {
            var gameObject = gameObjects[index];
            gameObject.Position = new PointF(
                gameObject.Position.X, gameObject.Position.Y + (float)dt.TotalSeconds * gameObject.Speed);
            if (gameObject.Position.Y > ScreenSize.Height)
            {
                gameObjects.RemoveAt(index);
                continue;
            }

            if (RectangleF.Intersect(
                new RectangleF(Player.Position, Player.Size),
                new RectangleF(gameObject.Position, gameObject.Size)) != RectangleF.Empty)
            {
                if (gameObject.IsAsteroid)
                {
                    IsRunning = false;
                    Console.WriteLine($"GAME OVER! Score - {score}");
                    break;
                }
                else
                {
                    score += 10;
                    gameObjects.RemoveAt(index);
                    continue;
                }
            }
        }
    }

    public void Render()
    {
        SDL.SetRenderDrawColor(renderer, background.R, background.G, background.B, kSDLAlphaOpaque);
        SDL.RenderClear(renderer);
        foreach (var gameObject in gameObjects)
        {
            var objectColor = gameObject.Color;
            if (gameObject.Texture is not null)
            {
                SDL.SetTextureColorMod(gameObject.Texture.SdlTexture, objectColor.R, objectColor.G, objectColor.B);
                SDL.RenderTexture(
                    renderer, gameObject.Texture.SdlTexture,
                    new SDL.FRect
                    {
                        X = 0,
                        Y = 0,
                        W = gameObject.Size.Width,
                        H = gameObject.Size.Height,
                    },
                    new SDL.FRect
                    {
                        X = gameObject.Position.X,
                        Y = gameObject.Position.Y,
                        W = gameObject.Size.Width,
                        H = gameObject.Size.Height,
                    }
                );
            }
        }

        if (Player is not null && Player.Texture is not null)
        {
            SDL.SetTextureColorMod(Player.Texture.SdlTexture, Player.Color.R, Player.Color.G, Player.Color.B);
            SDL.RenderTexture(
                renderer, Player.Texture.SdlTexture,
                new SDL.FRect
                {
                    X = 0,
                    Y = 0,
                    W = Player.Size.Width,
                    H = Player.Size.Height,
                },
                new SDL.FRect
                {
                    X = Player.Position.X,
                    Y = Player.Position.Y,
                    W = Player.Size.Width,
                    H = Player.Size.Height,
                }
            );

        }

        var text = $"Score: {score}";
        var sdlText = TTF.CreateText(textEngine, font, text, (nuint)text.Length);
        TTF.DrawRendererText(sdlText, 0, 0);
        TTF.DestroyText(sdlText);

        SDL.RenderPresent(renderer);

        CountFPS();
    }
}