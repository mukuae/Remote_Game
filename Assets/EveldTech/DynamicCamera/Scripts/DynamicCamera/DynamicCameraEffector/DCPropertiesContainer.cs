using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Eveld.DynamicCamera
{
    /// <summary>
    /// Class to help with copying data for proper UNDO functionality
    /// </summary>
    public class DCPropertiesContainer
    {
        // shared fields of DCEffector
        private string name;
        private bool isEnabled;
        private float featherAmount;
        private bool invertFeatherRegion;

        private bool repel;
        private bool invertStrength;
        private bool distanceFromCenterEqualsStrength;
        private bool unilateralDisplacement;

        public bool useRegionAsBounds;

        // additional fields
        private float strength1 = 0;             // for all
        private float depthStrength1 = 0;        // for all

        private float strength2 = 0;             // for box effector
        private float depthStrength2 = 0;        // for box effector

        private float radius = 0;                // for circle, semicircle, torus and semi torus
        private float distanceOutward1 = 0;      // for torus and box effector
        private float distanceOutward2 = 0;      // for box effector

        private bool canCrossCenter = false;     // circle/semi circle effector
        private bool useCircleCaps = false;      // multi and semi torus effector
        private bool useAsLoop = false;          // multi effector


        public DCPropertiesContainer(DCEffector effector)
        {
            name = effector.name;
            isEnabled = effector.isEnabled;
            featherAmount = effector.featherAmount;
            invertFeatherRegion = effector.invertFeatherRegion;
            repel = effector.repel;
            invertStrength = effector.invertStrength;
            distanceFromCenterEqualsStrength = effector.distanceFromCenterEqualsStrength;
            unilateralDisplacement = effector.unilateralDisplacement;
            useRegionAsBounds = effector.useRegionAsBounds;

            if (effector is DCCircleEffector)
            {
                DCCircleEffector circleEffector = (DCCircleEffector)effector;
                strength1 = circleEffector.strength;
                depthStrength1 = circleEffector.depthStrength;
                canCrossCenter = circleEffector.displacementCanCrossCenter;
                radius = circleEffector.Radius;
            }

            if (effector is DCTorusEffector)
            {
                DCTorusEffector torusEffector = (DCTorusEffector)effector;
                strength1 = torusEffector.strength;
                depthStrength1 = torusEffector.depthStrength;
                distanceOutward1 = torusEffector.distanceOutward;
                radius = torusEffector.RadiusAtCenterLine;

                if (torusEffector is DCSemiTorusEffector)
                {
                    useCircleCaps = ((DCSemiTorusEffector)torusEffector).useStartAndEndCaps;
                }
            }

            if (effector is DCBoxEffector)
            {
                DCBoxEffector boxEffector = (DCBoxEffector)effector;
                strength1 = boxEffector.strength1;
                depthStrength1 = boxEffector.depthStrength1;
                distanceOutward1 = boxEffector.distance1;

                strength2 = boxEffector.strength2;
                depthStrength2 = boxEffector.depthStrength2;
                distanceOutward2 = boxEffector.distance2;
            }

            if (effector is DCMultiEffector)
            {
                DCMultiEffector multiEffector = (DCMultiEffector)effector;

                useAsLoop = multiEffector.useAsLoop;
                useCircleCaps = multiEffector.useStartAndEndCaps;
            }
        }


        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                DCPropertiesContainer other = (DCPropertiesContainer)obj;
                bool isEqual = name == other.name && isEnabled == other.isEnabled && featherAmount == other.featherAmount && invertFeatherRegion == other.invertFeatherRegion
                            && repel == other.repel && invertStrength == other.invertStrength && distanceFromCenterEqualsStrength == other.distanceFromCenterEqualsStrength
                            && unilateralDisplacement == other.unilateralDisplacement && useRegionAsBounds == other.useRegionAsBounds
                            // additional fields
                            && strength1 == other.strength1 && depthStrength1 == other.depthStrength1 && strength2 == other.strength2 && depthStrength2 == other.depthStrength2
                            && distanceOutward1 == other.distanceOutward1 && distanceOutward2 == other.distanceOutward2 && radius == other.radius
                            && canCrossCenter == other.canCrossCenter && useCircleCaps == other.useCircleCaps && useAsLoop == other.useAsLoop;               
                return isEqual;
            }
        }

        public override int GetHashCode()
        {
            return name.GetHashCode();
        }

        public void AssignPropertiesTo(DCEffector effector)
        {
            effector.name = name;
            effector.isEnabled = isEnabled;
            effector.featherAmount = featherAmount;
            effector.invertFeatherRegion = invertFeatherRegion;
            effector.repel = repel;
            effector.invertStrength = invertStrength;
            effector.distanceFromCenterEqualsStrength = distanceFromCenterEqualsStrength;
            effector.unilateralDisplacement = unilateralDisplacement;
            effector.useRegionAsBounds = useRegionAsBounds;

            if (effector is DCCircleEffector)
            {
                DCCircleEffector circleEffector = (DCCircleEffector)effector;
                circleEffector.strength = strength1;
                circleEffector.depthStrength = depthStrength1;
                circleEffector.displacementCanCrossCenter = canCrossCenter;
                circleEffector.SetHandleByRadius(radius);
            }

            if (effector is DCTorusEffector)
            {
                DCTorusEffector torusEffector = (DCTorusEffector)effector;
                torusEffector.strength = strength1;
                torusEffector.depthStrength = depthStrength1;
                torusEffector.distanceOutward = distanceOutward1;
                torusEffector.SetHandleByCenterLineRadius(radius);
                if (torusEffector is DCSemiTorusEffector)
                {
                    ((DCSemiTorusEffector)torusEffector).useStartAndEndCaps = useCircleCaps;
                }
            }

            if (effector is DCBoxEffector)
            {
                DCBoxEffector boxEffector = (DCBoxEffector)effector;
                boxEffector.strength1 = strength1;
                boxEffector.depthStrength1 = depthStrength1;
                boxEffector.distance1 = distanceOutward1;

                boxEffector.strength2 = strength2;
                boxEffector.depthStrength2 = depthStrength2;
                boxEffector.distance2 = distanceOutward2;
            }

            if (effector is DCMultiEffector)
            {
                DCMultiEffector multiEffector = (DCMultiEffector)effector;

                multiEffector.useAsLoop = useAsLoop;
                multiEffector.useStartAndEndCaps = useCircleCaps;
            }
        }


    }

}
