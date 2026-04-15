using Eveld.DynamicCamera;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Eveld.DynamicCamera
{
    /// <summary>
    /// This class is used in the Unity Editor as container for created effectors. In runtime this class is used for displacing a point if it is inside an effector.
    /// </summary>
    [System.Serializable]
    public class DCEffectorManager : MonoBehaviour
    {
        // global list that holds all the effectos across the managers
        private static List<DCEffector> globalEffectorList = new List<DCEffector>();

        // all local lists of the effectors by type, this is done this way because the DCEffector class is abstract and cant be serialized. You also cant use serialization with polymorphism
        [HideInInspector]
        public List<DCCircleEffector> circleEffectorList = new List<DCCircleEffector>();
        [HideInInspector]
        public List<DCSemiCircleEffector> semiCircleEffectorList = new List<DCSemiCircleEffector>();
        [HideInInspector]
        public List<DCTorusEffector> torusEffectorList = new List<DCTorusEffector>();
        [HideInInspector]
        public List<DCSemiTorusEffector> semiTorusEffectorList = new List<DCSemiTorusEffector>();
        [HideInInspector]
        public List<DCBoxEffector> boxEffectorList = new List<DCBoxEffector>();
        [HideInInspector]
        public List<DCMultiEffector> multiEffectorList = new List<DCMultiEffector>();


        private void OnEnable()
        {
            // adds all local lists to the global list, so that the list is filled when we call start in other script that need the effectors
            AddCurrentListToGlobalList(circleEffectorList);
            AddCurrentListToGlobalList(semiCircleEffectorList);
            AddCurrentListToGlobalList(torusEffectorList);
            AddCurrentListToGlobalList(semiTorusEffectorList);
            AddCurrentListToGlobalList(boxEffectorList);
            AddCurrentListToGlobalList(multiEffectorList);
        }



        /// <summary>
        /// Checks if the specified point is inside an effector and calculates the displacement with an option to make it weighted if there are overlapping effectors.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="effectorOutputData"></param>
        /// <param name="takeOverlapsIntoAccount">Can be affected by multiple overlapping effectors, else it returns on the first hit</param>
        /// <param name="limitToMaxStrength">Limits the maximum displacement to the effects of the maximum strength it encountered if the position is affected by multiple effectors</param>
        /// <returns></returns>
        public static bool GetDisplacementAt(Vector2 position, out DCEffectorOutputData effectorOutputData, bool takeOverlapsIntoAccount = false, bool limitToMaxStrength = true)
        {
            effectorOutputData.displacement = Vector3.zero;
            effectorOutputData.lockedXY = false;
            effectorOutputData.influence = 0;

            if (takeOverlapsIntoAccount)
            {
                List<DCEffector> insideEffectorList = new List<DCEffector>();
                for (int i = 0; i < globalEffectorList.Count; i++)
                {
                    if (globalEffectorList[i].IsInsideEffector(position))
                    {
                        insideEffectorList.Add(globalEffectorList[i]);
                    }
                }

                if (insideEffectorList.Count == 0) return false;    // no point was inside

                DCEffector.GetWeightedAverageDisplacement(position, insideEffectorList, limitToMaxStrength, out effectorOutputData);
                return true;
            }
            else
            {
                // return first point we find
                for (int i = 0; i < globalEffectorList.Count; i++)
                {
                    if (globalEffectorList[i].GetDisplacementAt(position, out effectorOutputData)) return true;
                }

                return false;
            }
        }


        /// <summary>
        /// Gets the displacement at the position if it was affected by an effector and returns an array of effectors that had the position inside of them.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="effectorOutputData"></param>
        /// <param name="takeOverlapsIntoAccount">Can be affected by multiple overlapping effectors, else it returns on the first hit</param>
        /// <param name="limitToMaxStrength">Limits the maximum displacement to the effects of the maximum strength it encountered if the position is affected by multiple effectors</param>
        /// <returns>An array of effectors, if array length == 0 not inside any effector</returns>
        public static DCEffector[] GetDisplacementAndEffectorsAt(Vector2 position, out DCEffectorOutputData effectorOutputData, bool takeOverlapsIntoAccount = false, bool limitToMaxStrength = true)
        {
            effectorOutputData.displacement = Vector3.zero;
            effectorOutputData.lockedXY = false;
            effectorOutputData.influence = 0;

            if (takeOverlapsIntoAccount)
            {
                List<DCEffector> insideEffectorList = new List<DCEffector>();
                for (int i = 0; i < globalEffectorList.Count; i++)
                {
                    if (globalEffectorList[i].IsInsideEffector(position))
                    {
                        insideEffectorList.Add(globalEffectorList[i]);
                    }
                }

                if (insideEffectorList.Count == 0) return insideEffectorList.ToArray();    // no point was inside

                DCEffector.GetWeightedAverageDisplacement(position, insideEffectorList, limitToMaxStrength, out effectorOutputData);
                return insideEffectorList.ToArray();
            }
            else
            {
                // return first point we find
                for (int i = 0; i < globalEffectorList.Count; i++)
                {
                    if (globalEffectorList[i].GetDisplacementAt(position, out effectorOutputData))
                    {
                        return new DCEffector[1] { globalEffectorList[i] };
                    }
                }
            }

            return new DCEffector[0];
        }




        public void PrintListCounts()
        {
            Debug.Log("CircleEffectorListCount = " + circleEffectorList.Count +
                "\nSemiCircleEffectorListCount = " + semiCircleEffectorList.Count +
                "\nTorusEffectorListCount = " + torusEffectorList.Count +
                "\nSemiTorusEffectorListCount = " + semiTorusEffectorList.Count +
                "\nBoxEffectorListCount = " + boxEffectorList.Count +
                "\nMultiEffectorListCount = " + multiEffectorList.Count +
                "\nGlobalListCount = " + globalEffectorList.Count);
        }


        private void OnDisable()
        {
            // remove the local list entries from the global list, but keep the local lists
            RemoveLocalListsFromGlobalList(circleEffectorList, false);
            RemoveLocalListsFromGlobalList(semiCircleEffectorList, false);
            RemoveLocalListsFromGlobalList(torusEffectorList, false);
            RemoveLocalListsFromGlobalList(semiTorusEffectorList, false);
            RemoveLocalListsFromGlobalList(boxEffectorList, false);
            RemoveLocalListsFromGlobalList(multiEffectorList, false);
        }

        private void OnDestroy()
        {
            // Technically OnDisable is called before OnDestoy by Unity, so we would only need to clear the local lists here.
            // For safety it checks the global list again and clears the local lists afterwards
            RemoveLocalListsFromGlobalList(circleEffectorList, true);
            RemoveLocalListsFromGlobalList(semiCircleEffectorList, true);
            RemoveLocalListsFromGlobalList(torusEffectorList, true);
            RemoveLocalListsFromGlobalList(semiTorusEffectorList, true);
            RemoveLocalListsFromGlobalList(boxEffectorList, true);
            RemoveLocalListsFromGlobalList(multiEffectorList, true);
        }


        /// <summary>
        /// Gets the first effector by the specified name from the global list
        /// </summary>
        /// <param name="effectorName"></param>
        /// <returns>The effector or null</returns>
        public static DCEffector GetEffectorByName(string effectorName)
        {
            for (int i = 0; i < globalEffectorList.Count; i++)
            {
                if (globalEffectorList[i].name == effectorName)
                {
                    return globalEffectorList[i];
                }
            }

            return null;
        }


        public static void AddEffectorToGlobalList(DCEffector effector)
        {
            if (CheckIfEffectorIsNotInGlobalList(effector))
            {
                globalEffectorList.Add(effector);
            }
        }

        public static void RemoveEffectorFromGlobalList(int effectorID)
        {
            for (int i = 0; i < globalEffectorList.Count; i++)
            {
                if (globalEffectorList[i].GetID() == effectorID)
                {
                    globalEffectorList.RemoveAt(i);
                    break;
                }
            }
        }

        public static void RemoveEffectorFromGlobalList(DCEffector effector)
        {
            RemoveEffectorFromGlobalList(effector.GetID());
        }


        /// <summary>
        /// Adds a list to the global list. It does not do a check if its already added to the global list.
        /// </summary>
        /// <typeparam name="T">expected DCEffector</typeparam>
        /// <param name="currentList"></param>
        private void AddCurrentListToGlobalList<T>(List<T> currentList)
        {
            for (int i = 0; i < currentList.Count; i++)
            {
                DCEffector effector = currentList[i] as DCEffector;
                if (effector != null)
                {
                    effector.UpdateEffector();
                    globalEffectorList.Add(effector);
                }
            }
        }

        private void RemoveLocalListsFromGlobalList<T>(List<T> currentList, bool clearLocalList)
        {
            for (int i = 0; i < currentList.Count; i++)
            {
                DCEffector effector = currentList[i] as DCEffector;
                if (effector != null)
                {
                    for (int j = 0; j < globalEffectorList.Count; j++)
                    {
                        if (globalEffectorList[j].Equals(effector))
                        {
                            globalEffectorList.RemoveAt(j);
                            break;
                        }
                    }
                }
            }

            if (clearLocalList)
            {
                currentList.Clear();
            }
        }


        /// <summary>
        /// Checks if we already added an effector with its ID to the list
        /// </summary>
        /// <param name="effector"></param>
        /// <returns></returns>
        private static bool CheckIfEffectorIsNotInGlobalList(DCEffector effector)
        {
            for (int i = 0; i < globalEffectorList.Count; i++)
            {
                if (globalEffectorList[i].Equals(effector))
                {
                    return false;
                }
            }

            return true;
        }


    }

}
