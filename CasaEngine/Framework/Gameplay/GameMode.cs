using CasaEngine.Core.Log;
using CasaEngine.Framework.Entities;

namespace CasaEngine.Framework.Gameplay
{
    public class GameMode : Entity
    {
        private string _matchState = EnteringMap;
        public const string EnteringMap = "EnteringMap";
        public const string WaitingToStart = "WaitingToStart";
        public const string InProgress = "InProgress";
        public const string WaitingPostMatch = "WaitingPostMatch";
        public const string LeavingMap = "LeavingMap";
        public const string Aborted = "Aborted";

        public event EventHandler<string> GameStateChanged;
        public World.World World { get; private set; }

        /** Returns the current match state, this is an accessor to protect the state machine flow */
        public string MatchState
        {
            get => _matchState;
            set
            {
                if (_matchState == value)
                {
                    return;
                }

                Logs.WriteInfo($"Match State Changed from {MatchState} to {value}");

                _matchState = value;

                OnMatchStateSet();
            }
        }

        public void StartPlay()
        {
            // Don't call super, this class handles begin play/match start itself
            if (MatchState == EnteringMap)
            {
                MatchState = WaitingToStart;
            }

            // Check to see if we should immediately transfer to match start
            if (MatchState == WaitingToStart && ReadyToStartMatch())
            {
                StartMatch();
            }
        }

        /** Returns true if the match state is InProgress or other gameplay state */
        public virtual bool IsMatchInProgress()
        {
            return MatchState == InProgress;
        }

        /** Transition from WaitingToStart to InProgress. You can call this manually, will also get called if ReadyToStartMatch returns true */
        public virtual void StartMatch()
        {
            if (HasMatchStarted())
            {
                // Already started
                return;
            }

            MatchState = InProgress;
        }

        /** Transition from InProgress to WaitingPostMatch. You can call this manually, will also get called if ReadyToEndMatch returns true */
        public virtual void EndMatch()
        {
            if (!IsMatchInProgress())
            {
                return;
            }

            MatchState = WaitingPostMatch;
        }

        public void InitGame(World.World world)
        {
            World = world;
            // Save Options for future use
            //OptionsString = Options;
            /*
            FActorSpawnParameters SpawnInfo;
            SpawnInfo.Instigator = GetInstigator();
            SpawnInfo.ObjectFlags |= RF_Transient;	// We never want to save game sessions into a map
            GameSession = World->SpawnActor<AGameSession>(GetGameSessionClass(), SpawnInfo);
            GameSession->InitOptions(Options);

            FGameModeEvents::GameModeInitializedEvent.Broadcast(this);
            if (GetNetMode() != NM_Standalone)
            {
                // Attempt to login, returning true means an async login is in flight
                if (!UOnlineEngineInterface::Get()->DoesSessionExist(World, GameSession->SessionName) && 
                    !GameSession->ProcessAutoLogin())
                {
                    GameSession->RegisterServer();
                }
            }
            */

            MatchState = EnteringMap;
            /*
            if (GameStateClass == null)
            {
                UE_LOG(LogGameMode, Error, TEXT("GameStateClass is not set, falling back to AGameState."));
                GameStateClass = AGameState::StaticClass();
            }
            else if (!GameStateClass->IsChildOf<AGameState>())
            {
                UE_LOG(LogGameMode, Error, TEXT("Mixing AGameStateBase with AGameMode is not compatible. Change AGameStateBase subclass (%s) to derive from AGameState, or make both derive from Base"), *GameStateClass->GetName());
            }

            // Bind to delegates
            FGameDelegates::Get().GetPendingConnectionLostDelegate().AddUObject(this, &AGameMode::NotifyPendingConnectionLost);
            FGameDelegates::Get().GetPreCommitMapChangeDelegate().AddUObject(this, &AGameMode::PreCommitMapChange);
            FGameDelegates::Get().GetPostCommitMapChangeDelegate().AddUObject(this, &AGameMode::PostCommitMapChange);
            FGameDelegates::Get().GetHandleDisconnectDelegate().AddUObject(this, &AGameMode::HandleDisconnect);*/
        }

        /** Restart the game, by default travel to the current map */
        public virtual void RestartGame()
        {
            /*
            if (GameSession->CanRestartGame())
            {
                if (MatchState == LeavingMap)
                {
                    return;
                }

                World.ServerTravel("?Restart",GetTravelType());
            }
            */
        }

        /** Report that a match has failed due to unrecoverable error */
        public virtual void AbortMatch()
        {
            MatchState = Aborted;
        }

        public bool HasMatchStarted()
        {
            if (MatchState == EnteringMap || MatchState == WaitingToStart)
            {
                return false;
            }

            return true;
        }

        public bool HasMatchEnded()
        {
            if (MatchState == WaitingPostMatch || MatchState == LeavingMap)
            {
                return true;
            }

            return false;
        }

        public void Tick(float elapsedTime)
        {
            base.Update(elapsedTime);

            if (MatchState == WaitingToStart)
            {
                // Check to see if we should start the match
                if (ReadyToStartMatch())
                {
                    Logs.WriteInfo("GameMode returned ReadyToStartMatch");
                    StartMatch();
                }
            }
            if (MatchState == InProgress)
            {
                // Check to see if we should start the match
                if (ReadyToEndMatch())
                {
                    Logs.WriteInfo("GameMode returned ReadyToEndMatch");
                    EndMatch();
                }
            }
        }

        public void StartToLeaveMap()
        {
            MatchState = LeavingMap;
        }


        /** Overridable virtual function to dispatch the appropriate transition functions before GameState and Blueprints get SetMatchState calls. */
        protected virtual void OnMatchStateSet()
        {
            GameStateChanged?.Invoke(this, MatchState);

            // Call change callbacks
            if (MatchState == WaitingToStart)
            {
                HandleMatchIsWaitingToStart();
            }
            else if (MatchState == InProgress)
            {
                HandleMatchHasStarted();
            }
            else if (MatchState == WaitingPostMatch)
            {
                HandleMatchHasEnded();
            }
            else if (MatchState == LeavingMap)
            {
                HandleLeavingMap();
            }
            else if (MatchState == Aborted)
            {
                HandleMatchAborted();
            }
        }

        // Games should override these functions to deal with their game specific logic

        /** Called when the state transitions to WaitingToStart */
        protected virtual void HandleMatchIsWaitingToStart()
        {
            /*
            if (GameSession != null)
            {
                GameSession->HandleMatchIsWaitingToStart();
            }*/

            // Calls begin play on actors, unless we're about to transition to match start
            if (!ReadyToStartMatch())
            {
                //GetWorldSettings()->NotifyBeginPlay();
            }
        }

        /** Returns true if ready to Start Match. Games should override this */
        protected bool ReadyToStartMatch()
        {
            // If bDelayed Start is set, wait for a manual match start
            /*if (bDelayedStart)
            {
                return false;
            }*/

            // By default start when we have > 0 players
            if (MatchState == WaitingToStart)
            {
                /*if (NumPlayers + NumBots > 0)
                {
                    return true;
                }*/
            }

            return false;
        }

        /** Called when the state transitions to InProgress */
        protected virtual void HandleMatchHasStarted()
        {
            /*
            GameSession->HandleMatchHasStarted();

            // start human players first
            for (FConstPlayerControllerIterator Iterator = World.GetPlayerControllerIterator(); Iterator; ++Iterator)
            {
                APlayerController* PlayerController = Iterator->Get();
                if (PlayerController && (PlayerController->GetPawn() == null) && PlayerCanRestart(PlayerController))
                {
                    RestartPlayer(PlayerController);
                }
            }

            // Make sure level streaming is up to date before triggering NotifyMatchStarted
            GEngine->BlockTillLevelStreamingCompleted(World);

            // First fire BeginPlay, if we haven't already in waiting to start match
            GetWorldSettings()->NotifyBeginPlay();

            // Then fire off match started
            GetWorldSettings()->NotifyMatchStarted();
            */
            /*
            // if passed in bug info, send player to right location
            const FString BugLocString = UGameplayStatics::ParseOption(OptionsString, TEXT("BugLoc"));
            const FString BugRotString = UGameplayStatics::ParseOption(OptionsString, TEXT("BugRot"));
            if (!BugLocString.IsEmpty() || !BugRotString.IsEmpty())
            {
                for (FConstPlayerControllerIterator Iterator = GetWorld()->GetPlayerControllerIterator(); Iterator; ++Iterator)
                {
                    APlayerController* PlayerController = Iterator->Get();
                    if (PlayerController && PlayerController->CheatManager != null)
                    {
                        PlayerController->CheatManager->BugItGoString(BugLocString, BugRotString);
                    }
                }
            }

            if (IsHandlingReplays() && GetGameInstance() != null)
            {
                GetGameInstance()->StartRecordingReplay(World.Name);
            }*/
        }

        /** Returns true if ready to End Match. Games should override this */
        protected virtual bool ReadyToEndMatch()
        {
            return false;
        }

        /** Called when the map transitions to WaitingPostMatch */
        protected virtual void HandleMatchHasEnded()
        {
            /*GameSession->HandleMatchHasEnded();

            if (IsHandlingReplays() && GetGameInstance() != null)
            {
                GetGameInstance()->StopRecordingReplay();
            }*/
        }

        /** Called when the match transitions to LeavingMap */
        protected virtual void HandleLeavingMap()
        {
        }

        /** Called when the match transitions to Aborted */
        protected virtual void HandleMatchAborted()
        {
        }

        /*
        public void RestartPlayer(AController* NewPlayer)
        {
            if (NewPlayer == null || NewPlayer->IsPendingKillPending())
            {
                return;
            }

            Entity StartSpot = FindPlayerStart(NewPlayer);

            // If a start spot wasn't found,
            if (StartSpot == null)
            {
                // Check for a previously assigned spot
                if (NewPlayer->StartSpot != null)
                {
                    StartSpot = NewPlayer->StartSpot.Get();
                    Logs.WriteWarning("RestartPlayer: Player start not found, using last start spot");
                }	
            }

            RestartPlayerAtPlayerStart(NewPlayer, StartSpot);
        }

        public void RestartPlayerAtPlayerStart(AController* NewPlayer, Entity StartSpot)
        {
            if (NewPlayer == null || NewPlayer->IsPendingKillPending())
            {
                return;
            }

            if (!StartSpot)
            {
                Logs.WriteWarning("RestartPlayerAtPlayerStart: Player start not found"));
                return;
            }

            FRotator SpawnRotation = StartSpot->GetActorRotation();

            Logs.WriteVerbose("RestartPlayerAtPlayerStart %s"), (NewPlayer && NewPlayer->PlayerState) ? *NewPlayer->PlayerState->GetPlayerName() : TEXT("Unknown"));

            if (MustSpectate(Cast<APlayerController>(NewPlayer)))
            {
                Logs.WriteVerbose("RestartPlayerAtPlayerStart: Tried to restart a spectator-only player!"));
                return;
            }

            if (NewPlayer->GetPawn() != null)
            {
                // If we have an existing pawn, just use it's rotation
                SpawnRotation = NewPlayer->GetPawn()->GetActorRotation();
            }
            else if (GetDefaultPawnClassForController(NewPlayer) != null)
            {
                // Try to create a pawn to use of the default class for this player
                APawn* NewPawn = SpawnDefaultPawnFor(NewPlayer, StartSpot);
                if (IsValid(NewPawn))
                {
                    NewPlayer->SetPawn(NewPawn);
                }
            }
	
            if (!IsValid(NewPlayer->GetPawn()))
            {
                FailedToRestartPlayer(NewPlayer);
            }
            else
            {
                // Tell the start spot it was used
                InitStartSpot(StartSpot, NewPlayer);

                FinishRestartPlayer(NewPlayer, SpawnRotation);
            }
        }*/
    }
}
