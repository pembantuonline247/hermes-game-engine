using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hermes.GameEngine.Core
{
    /// <summary>
    /// A generic, type-safe finite state machine base class for managing game states.
    /// Supports state transitions with optional enter/exit callbacks.
    /// </summary>
    /// <typeparam name="TStateId">The type used to identify states (e.g., int, string, enum).</typeparam>
    public class StateMachine<TStateId>
    {
        /// <summary>
        /// Defines the contract for a single state within the state machine.
        /// </summary>
        public interface IState
        {
            /// <summary>
            /// Called once when entering this state.
            /// </summary>
            void Enter();

            /// <summary>
            /// Called every frame while this state is active.
            /// </summary>
            void Update();

            /// <summary>
            /// Called once when exiting this state.
            /// </summary>
            void Exit();
        }

        /// <summary>
        /// Event raised whenever a state transition occurs.
        /// Parameters: (fromStateId, toStateId).
        /// </summary>
        public event Action<TStateId, TStateId> OnStateChanged;

        private readonly Dictionary<TStateId, IState> _states = new Dictionary<TStateId, IState>();

        private TStateId _currentStateId;
        private IState _currentState;
        private bool _isInitialized;

        /// <summary>
        /// The identifier of the currently active state.
        /// Returns the default value of TStateId if the machine has not been initialized.
        /// </summary>
        public TStateId CurrentStateId
        {
            get
            {
                if (!_isInitialized)
                {
                    Debug.LogWarning("[StateMachine] CurrentStateId accessed before initialization.");
                    return default;
                }
                return _currentStateId;
            }
        }

        /// <summary>
        /// Returns true if the state machine has been initialized with a starting state.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Registers a state with the given identifier.
        /// Overwrites any previously registered state for the same id.
        /// </summary>
        /// <param name="id">Unique identifier for the state.</param>
        /// <param name="state">The state instance implementing <see cref="IState"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="state"/> is null.</exception>
        public void RegisterState(TStateId id, IState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state), "[StateMachine] Cannot register a null state.");

            if (_states.ContainsKey(id))
            {
                Debug.LogWarning($"[StateMachine] State '{id}' is being overwritten.");
            }

            _states[id] = state;
        }

        /// <summary>
        /// Removes a previously registered state.
        /// </summary>
        /// <param name="id">The identifier of the state to remove.</param>
        /// <returns>True if the state was found and removed; false otherwise.</returns>
        public bool UnregisterState(TStateId id)
        {
            return _states.Remove(id);
        }

        /// <summary>
        /// Initializes the state machine and transitions to the specified starting state.
        /// Must be called after all states have been registered.
        /// </summary>
        /// <param name="startStateId">The identifier of the state to transition to first.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <paramref name="startStateId"/> has not been registered.
        /// </exception>
        public void Initialize(TStateId startStateId)
        {
            if (!_states.TryGetValue(startStateId, out IState startState))
            {
                throw new InvalidOperationException(
                    $"[StateMachine] Cannot initialize: state '{startStateId}' has not been registered.");
            }

            _isInitialized = true;
            _currentStateId = startStateId;
            _currentState = startState;

            Debug.Log($"[StateMachine] Initialized with state '{startStateId}'.");
            _currentState.Enter();
        }

        /// <summary>
        /// Transitions to a new state, calling Exit() on the current state and Enter() on the target state.
        /// No operation is performed if the machine is not initialized or if the target state is the same as
        /// the current state (unless <paramref name="forceTransition"/> is true).
        /// </summary>
        /// <param name="newStateId">The identifier of the state to transition to.</param>
        /// <param name="forceTransition">
        /// If true, forces a transition even if the target state matches the current state.
        /// </param>
        /// <returns>True if the transition was performed; false otherwise.</returns>
        public bool TransitionTo(TStateId newStateId, bool forceTransition = false)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[StateMachine] Cannot transition: state machine has not been initialized. Call Initialize() first.");
                return false;
            }

            if (!forceTransition && EqualityComparer<TStateId>.Default.Equals(_currentStateId, newStateId))
            {
                Debug.Log($"[StateMachine] Already in state '{newStateId}', skipping transition.");
                return false;
            }

            if (!_states.TryGetValue(newStateId, out IState newState))
            {
                Debug.LogError($"[StateMachine] Cannot transition to '{newStateId}': state has not been registered.");
                return false;
            }

            TStateId previousStateId = _currentStateId;

            Debug.Log($"[StateMachine] Transitioning from '{_currentStateId}' to '{newStateId}'.");

            _currentState?.Exit();

            _currentStateId = newStateId;
            _currentState = newState;

            _currentState.Enter();

            OnStateChanged?.Invoke(previousStateId, newStateId);
            return true;
        }

        /// <summary>
        /// Calls Update() on the currently active state. Should be invoked from MonoBehaviour.Update().
        /// </summary>
        public void Update()
        {
            if (!_isInitialized || _currentState == null)
                return;

            _currentState.Update();
        }

        /// <summary>
        /// Shuts down the state machine, calling Exit() on the current state and resetting internal state.
        /// </summary>
        public void Shutdown()
        {
            if (!_isInitialized)
                return;

            Debug.Log($"[StateMachine] Shutting down from state '{_currentStateId}'.");
            _currentState?.Exit();
            _currentState = null;
            _currentStateId = default;
            _isInitialized = false;
        }

        /// <summary>
        /// Removes all registered states without calling exit.
        /// Use Shutdown() for a clean teardown.
        /// </summary>
        public void Clear()
        {
            _states.Clear();
        }
    }
}
