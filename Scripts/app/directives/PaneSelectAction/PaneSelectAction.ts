﻿/// <reference path="../../_all.ts" />

module dockyard.directives.paneSelectAction {
    'use strict';

    export enum MessageType {
        PaneSelectAction_ActionUpdated,
        PaneSelectAction_Render,
        PaneSelectAction_Hide,
        PaneSelectAction_UpdateAction,
        PaneSelectAction_ActionTypeSelected,
        PaneSelectAction_InitiateSaveAction
    }

    export class ActionTypeSelectedEventArgs {
        public action: interfaces.IActionDTO

        constructor(action: interfaces.IActionDTO) {
            // Clone Action to prevent any issues due to possible mutation of source object
            this.action = angular.extend({}, action);
        }
    }

    export class ActionUpdatedEventArgs extends ActionEventArgsBase {
        public isTempId: boolean;
        public actionName: string;

        constructor(criteriaId: number, actionId: number, isTempId: boolean, actionName: string) {
            super(criteriaId, actionId);
            this.isTempId = isTempId;
            this.actionName = actionName;
        }
    }

    export class RenderEventArgs {
        public processNodeTemplateId: number;
        public id: number;
        public isTempId: boolean;
        public actionListId: number;

        constructor(
            processNodeTemplateId: number,
            id: number,
            isTemp: boolean,
            actionListId: number) {

            this.processNodeTemplateId = processNodeTemplateId;
            this.id = id;
            this.isTempId = isTemp;
            this.actionListId = actionListId;
        }
    }

    export class UpdateActionEventArgs extends ActionEventArgsBase {
        public isTempId: boolean;

        constructor(criteriaId: number, actionId: number, isTempId: boolean) {
            super(criteriaId, actionId);
            this.isTempId = isTempId;
        }
    }

    export class ActionRemovedEventArgs {
        public id: number;
        public isTempId: boolean;

        constructor(id: number, isTempId: boolean) {
            this.id = id;
            this.isTempId = isTempId;
        }
    }

    export class PaneSelectActionController {
        actionTypes: Array<model.ActivityTemplate> = []
        public static $inject = [
            '$scope',
            '$http'
        ];
        constructor(private $scope, private $http) {
            $scope.actionTypes = [];
            $http.get('/activities/available')
                .then(function (resp) {
                    angular.forEach(resp.data, function (it) {
                        $scope.actionTypes.push(
                            new model.ActivityTemplate(
                                it.id,
                                it.name,
                                it.version,
                                it.componentActivities,
                                it.category
                                )
                            );
                    });
                });

            $scope.actionTypeSelected = function (actionType) {
                $scope.$close(actionType);
            }
        }
    }

    app.controller('PaneSelectActionController', PaneSelectActionController);
}